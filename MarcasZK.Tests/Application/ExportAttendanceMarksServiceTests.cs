using System;
using System.Collections.Generic;
using FluentAssertions;
using MarcasZK.Application;
using MarcasZK.Domain;
using Moq;
using Xunit;

namespace MarcasZK.Tests.Application
{
    public class ExportAttendanceMarksServiceTests
    {
        private readonly Mock<IDeviceReader> _reader;
        private readonly Mock<IMarkExporter> _exporter;
        private readonly Mock<ILogger> _logger;
        private readonly Mock<IDeviceCleaner> _cleaner;
        private readonly ExportAttendanceMarksService _sut;

        public ExportAttendanceMarksServiceTests()
        {
            _reader = new Mock<IDeviceReader>();
            _exporter = new Mock<IMarkExporter>();
            _logger = new Mock<ILogger>();
            _cleaner = new Mock<IDeviceCleaner>();
            _sut = new ExportAttendanceMarksService(
                _reader.Object, _exporter.Object, _logger.Object, _cleaner.Object);
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static Device CreateDevice(
            string nombre = "TEST",
            string ip = "192.168.1.1",
            int port = 4370,
            bool borrarMarcas = false)
        {
            return new Device
            {
                Nombre = nombre,
                Origen = "Test",
                BorrarMarcas = borrarMarcas,
                Connection = new DeviceConnection { Ip = ip, Port = port, Password = "" }
            };
        }

        private static ReadMarksResult SuccessResult(params AttendanceMark[] marks)
        {
            return new ReadMarksResult
            {
                Success = true,
                Marks = new List<AttendanceMark>(marks)
            };
        }

        private static ReadMarksResult FailResult(
            ReadErrorType errorType, int errorCode = 0, string message = "")
        {
            return new ReadMarksResult
            {
                Success = false,
                ErrorType = errorType,
                ErrorCode = errorCode,
                ErrorMessage = message
            };
        }

        private static AttendanceMark MakeMark(string employeeId = "EMP001")
        {
            return new AttendanceMark
            {
                Origen = "Test",
                EmployeeId = employeeId,
                Timestamp = new DateTime(2024, 1, 15, 8, 0, 0),
                Flag = "-"
            };
        }

        private static ClearMarksResult MakeClearResult(
            bool success, int errorCode = 0, string message = "")
        {
            return new ClearMarksResult
            {
                Success = success,
                ErrorCode = errorCode,
                ErrorMessage = message
            };
        }

        // ─── Group 1: Connection Failures ─────────────────────────────────────

        [Fact]
        public void Given_DeviceFailsWithConnectionError_When_Execute_Then_ReturnsZero()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -1));

            int result = _sut.Execute(new[] { device }, "output");

            result.Should().Be(0);
        }

        [Fact]
        public void Given_DeviceFailsWithConnectionError_When_Execute_Then_LogsSDKNotInitialized()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -1));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("SDK no inicializado"))),
                Times.Once());
        }

        [Fact]
        public void Given_DeviceFailsWithReadDataError_When_Execute_Then_LogsNetworkConnectionError()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.ReadData, errorCode: -2));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("error de conexión de red"))),
                Times.Once());
        }

        [Fact]
        public void Given_DeviceFailsWithComNotRegisteredError_When_Execute_Then_LogsComErrorVerbatim()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.ComNotRegistered, message: "COM not registered"));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("Error COM: COM not registered"))),
                Times.Once());
        }

        [Fact]
        public void Given_DeviceFailsWithUnexpectedError_When_Execute_Then_LogsUnexpectedErrorMessage()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Unexpected, message: "boom"));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("Error inesperado: boom"))),
                Times.Once());
        }

        [Fact]
        public void Given_DeviceFailsToConnect_When_Execute_Then_CleanerIsNeverCalled()
        {
            var device = CreateDevice(borrarMarcas: true);
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -1));

            _sut.Execute(new[] { device }, "output");

            _cleaner.Verify(c => c.ClearMarks(It.IsAny<Device>()), Times.Never());
        }

        // ─── Group 2: Zero Marks ──────────────────────────────────────────────

        [Fact]
        public void Given_DeviceReadsSuccessfullyButZeroMarks_When_Execute_Then_ReturnsZero()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device)).Returns(SuccessResult());

            int result = _sut.Execute(new[] { device }, "output");

            result.Should().Be(0);
        }

        [Fact]
        public void Given_DeviceReadsSuccessfullyButZeroMarks_When_Execute_Then_LogsNoNewData()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device)).Returns(SuccessResult());

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("No hay datos nuevos"))),
                Times.Once());
        }

        [Fact]
        public void Given_DeviceReadsZeroMarksAndBorrarMarcasTrue_When_Execute_Then_CleanerNotCalled()
        {
            var device = CreateDevice(borrarMarcas: true);
            _reader.Setup(r => r.ReadMarks(device)).Returns(SuccessResult());

            _sut.Execute(new[] { device }, "output");

            _cleaner.Verify(c => c.ClearMarks(It.IsAny<Device>()), Times.Never());
        }

        // ─── Group 3: Successful Export ───────────────────────────────────────

        [Fact]
        public void Given_DeviceReadsThreeMarks_When_Execute_Then_ExportCalledThreeTimes()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2"), MakeMark("E3")));

            _sut.Execute(new[] { device }, "output");

            _exporter.Verify(e => e.Export(It.IsAny<AttendanceMark>(), "output"), Times.Exactly(3));
        }

        [Fact]
        public void Given_DeviceReadsThreeMarks_When_Execute_Then_ReturnsThree()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2"), MakeMark("E3")));

            int result = _sut.Execute(new[] { device }, "output");

            result.Should().Be(3);
        }

        [Fact]
        public void Given_DeviceReadsThreeMarks_When_Execute_Then_LogsFinalCount()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2"), MakeMark("E3")));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("3 marcas descargadas"))),
                Times.Once());
        }

        // ─── Group 4: Export Exception ────────────────────────────────────────

        [Fact]
        public void Given_ExporterThrows_When_Execute_Then_LogsExportErrorMessage()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2"), MakeMark("E3")));
            _exporter.Setup(e => e.Export(It.IsAny<AttendanceMark>(), It.IsAny<string>()))
                     .Throws(new InvalidOperationException("disk full"));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("Error exportando marcas: disk full"))),
                Times.Once());
        }

        [Fact]
        public void Given_ExporterThrowsAndBorrarMarcasTrue_When_Execute_Then_CleanerNotCalled()
        {
            var device = CreateDevice(borrarMarcas: true);
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1")));
            _exporter.Setup(e => e.Export(It.IsAny<AttendanceMark>(), It.IsAny<string>()))
                     .Throws(new InvalidOperationException("disk full"));

            _sut.Execute(new[] { device }, "output");

            _cleaner.Verify(c => c.ClearMarks(It.IsAny<Device>()), Times.Never());
        }

        // ─── Group 5: Clear After Export ─────────────────────────────────────

        [Fact]
        public void Given_BorrarMarcasTrueAndClearSucceeds_When_Execute_Then_LogsMarcasEliminadas()
        {
            var device = CreateDevice(borrarMarcas: true);
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2")));
            _cleaner.Setup(c => c.ClearMarks(device))
                    .Returns(MakeClearResult(success: true));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("marcas eliminadas del dispositivo"))),
                Times.Once());
        }

        [Fact]
        public void Given_BorrarMarcasTrueAndClearFails_When_Execute_Then_LogsErrorCodeAndMessage()
        {
            var device = CreateDevice(borrarMarcas: true);
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2")));
            _cleaner.Setup(c => c.ClearMarks(device))
                    .Returns(MakeClearResult(success: false, errorCode: 5, message: "timeout"));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("código=5") && s.Contains("timeout"))),
                Times.Once());
        }

        [Fact]
        public void Given_BorrarMarcasTrueAndCleanerIsNull_When_Execute_Then_LogsLimpiadorNoConfigurado()
        {
            var device = CreateDevice(borrarMarcas: true);
            var sut = new ExportAttendanceMarksService(
                _reader.Object, _exporter.Object, _logger.Object, cleaner: null);
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2")));

            sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("limpiador no configurado"))),
                Times.Once());
        }

        [Fact]
        public void Given_BorrarMarcasFalseAndCleanerInjected_When_Execute_Then_CleanerNeverCalled()
        {
            var device = CreateDevice(borrarMarcas: false);
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2")));
            _cleaner.Setup(c => c.ClearMarks(It.IsAny<Device>()))
                    .Returns(MakeClearResult(success: true));

            _sut.Execute(new[] { device }, "output");

            _cleaner.Verify(c => c.ClearMarks(It.IsAny<Device>()), Times.Never());
        }

        // ─── Group 6: Multiple Devices ────────────────────────────────────────

        [Fact]
        public void Given_TwoDevicesFirstFailsSecondExportsThree_When_Execute_Then_ReturnsThree()
        {
            var device1 = CreateDevice(nombre: "DEV1");
            var device2 = CreateDevice(nombre: "DEV2");
            _reader.Setup(r => r.ReadMarks(device1))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -1));
            _reader.Setup(r => r.ReadMarks(device2))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2"), MakeMark("E3")));

            int result = _sut.Execute(new[] { device1, device2 }, "output");

            result.Should().Be(3);
        }

        [Fact]
        public void Given_TwoDevicesBothSucceedWithDifferentCounts_When_Execute_Then_ReturnsSum()
        {
            var device1 = CreateDevice(nombre: "DEV1");
            var device2 = CreateDevice(nombre: "DEV2");
            _reader.Setup(r => r.ReadMarks(device1))
                   .Returns(SuccessResult(MakeMark("E1"), MakeMark("E2")));
            _reader.Setup(r => r.ReadMarks(device2))
                   .Returns(SuccessResult(MakeMark("E3"), MakeMark("E4"), MakeMark("E5")));

            int result = _sut.Execute(new[] { device1, device2 }, "output");

            result.Should().Be(5);
        }

        [Fact]
        public void Given_EmptyDeviceArray_When_Execute_Then_ReturnsZeroAndNoMocksCalled()
        {
            int result = _sut.Execute(new Device[0], "output");

            result.Should().Be(0);
            _reader.Verify(r => r.ReadMarks(It.IsAny<Device>()), Times.Never());
            _exporter.Verify(e => e.Export(It.IsAny<AttendanceMark>(), It.IsAny<string>()), Times.Never());
            _cleaner.Verify(c => c.ClearMarks(It.IsAny<Device>()), Times.Never());
        }

        // ─── Group 7: Error Code Translation ─────────────────────────────────

        [Fact]
        public void Given_ErrorCodeMinusOne_When_Execute_Then_LogContainsSDKNoInicializado()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -1));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("SDK no inicializado"))),
                Times.Once());
        }

        [Fact]
        public void Given_ErrorCodeMinusTwo_When_Execute_Then_LogContainsErrorDeConexionDeRed()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -2));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("error de conexión de red"))),
                Times.Once());
        }

        [Fact]
        public void Given_ErrorCodeMinusSix_When_Execute_Then_LogContainsContrasenaIncorrecta()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -6));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("contraseña incorrecta"))),
                Times.Once());
        }

        [Fact]
        public void Given_ErrorCodeMinusEight_When_Execute_Then_LogContainsTimeoutDeRecepcion()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -8));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("timeout de recepción"))),
                Times.Once());
        }

        [Fact]
        public void Given_ErrorCodeMinus307_When_Execute_Then_LogContainsTimeoutDeConexion()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: -307));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("timeout de conexión"))),
                Times.Once());
        }

        [Fact]
        public void Given_ErrorCodeZero_When_Execute_Then_LogContainsSinDatos()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: 0));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("sin datos"))),
                Times.Once());
        }

        [Fact]
        public void Given_UnknownErrorCode_When_Execute_Then_LogContainsCodigoDesconocido()
        {
            var device = CreateDevice();
            _reader.Setup(r => r.ReadMarks(device))
                   .Returns(FailResult(ReadErrorType.Connection, errorCode: 99));

            _sut.Execute(new[] { device }, "output");

            _logger.Verify(l => l.Log(
                device.Nombre,
                It.Is<string>(s => s.Contains("código desconocido"))),
                Times.Once());
        }
    }
}
