using System;
using FluentAssertions;
using MarcasZK.Domain;
using MarcasZK.Infrastructure;
using Xunit;

namespace MarcasZK.Tests.Infrastructure
{
    public class JsonConfigLoaderTests
    {
        // ─── Helper ──────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a {"Devices":[{...}]} JSON string with optional fields.
        /// Pass null for optional string fields to omit them entirely.
        /// </summary>
        private static string BuildJson(
            string ip = "10.0.0.1",
            int? port = 4370,
            string origen = "DEV",
            string nombre = "Test",
            string password = null,       // null = omit field
            bool? borrarMarcas = null)    // null = omit field
        {
            var fields = new System.Collections.Generic.List<string>();

            if (ip != null)
                fields.Add(string.Format("\"ip\": \"{0}\"", ip));

            if (port.HasValue)
                fields.Add(string.Format("\"port\": {0}", port.Value));

            if (origen != null)
                fields.Add(string.Format("\"origen\": \"{0}\"", origen));

            if (nombre != null)
                fields.Add(string.Format("\"nombre\": \"{0}\"", nombre));

            if (password != null)
                fields.Add(string.Format("\"password\": \"{0}\"", password));

            if (borrarMarcas.HasValue)
                fields.Add(string.Format("\"borrarMarcas\": {0}", borrarMarcas.Value ? "true" : "false"));

            string deviceJson = "{" + string.Join(", ", fields) + "}";
            return "{\"Devices\": [" + deviceJson + "]}";
        }

        // ─── Valid JSON: field mapping ────────────────────────────────────────

        [Fact]
        public void Given_ValidJsonWithOneDevice_When_LoadFromJson_Then_ReturnsOneDeviceWithAllFieldsMapped()
        {
            string json = BuildJson(
                ip: "192.168.0.5",
                port: 4370,
                origen: "ORIGEN1",
                nombre: "Dispositivo1",
                password: "secret",
                borrarMarcas: false);

            Device[] devices = JsonConfigLoader.LoadFromJson(json);

            devices.Should().HaveCount(1);
            Device d = devices[0];
            d.Connection.Ip.Should().Be("192.168.0.5");
            d.Connection.Port.Should().Be(4370);
            d.Connection.Password.Should().Be("secret");
            d.Origen.Should().Be("ORIGEN1");
            d.Nombre.Should().Be("Dispositivo1");
            d.BorrarMarcas.Should().BeFalse();
        }

        [Fact]
        public void Given_ValidJsonWithThreeDevices_When_LoadFromJson_Then_ReturnsThreeDevices()
        {
            string json = "{\"Devices\": [" +
                "{\"ip\":\"10.0.0.1\",\"port\":4370,\"origen\":\"A\",\"nombre\":\"D1\"}," +
                "{\"ip\":\"10.0.0.2\",\"port\":4371,\"origen\":\"B\",\"nombre\":\"D2\"}," +
                "{\"ip\":\"10.0.0.3\",\"port\":4372,\"origen\":\"C\",\"nombre\":\"D3\"}" +
                "]}";

            Device[] devices = JsonConfigLoader.LoadFromJson(json);

            devices.Should().HaveCount(3);
        }

        // ─── Missing "Devices" key ────────────────────────────────────────────

        [Fact]
        public void Given_JsonMissingDevicesKey_When_LoadFromJson_Then_ThrowsInvalidOperationExceptionWithDevices()
        {
            string json = "{\"Other\": []}";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Devices*");
        }

        [Fact]
        public void Given_JsonWithDevicesNull_When_LoadFromJson_Then_ThrowsInvalidOperationException()
        {
            string json = "{\"Devices\": null}";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>();
        }

        // ─── Missing required fields ──────────────────────────────────────────

        [Fact]
        public void Given_DeviceEntryMissingIp_When_LoadFromJson_Then_ThrowsWithIpInMessage()
        {
            // Build JSON omitting ip
            string json = "{\"Devices\": [{\"port\": 4370, \"origen\": \"DEV\", \"nombre\": \"D1\"}]}";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*ip*");
        }

        [Fact]
        public void Given_DeviceEntryMissingPort_When_LoadFromJson_Then_ThrowsWithPortInMessage()
        {
            string json = "{\"Devices\": [{\"ip\": \"10.0.0.1\", \"origen\": \"DEV\", \"nombre\": \"D1\"}]}";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*port*");
        }

        [Fact]
        public void Given_DeviceEntryMissingOrigen_When_LoadFromJson_Then_ThrowsWithOrigenInMessage()
        {
            string json = "{\"Devices\": [{\"ip\": \"10.0.0.1\", \"port\": 4370, \"nombre\": \"D1\"}]}";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*origen*");
        }

        [Fact]
        public void Given_DeviceEntryMissingNombre_When_LoadFromJson_Then_ThrowsWithNombreInMessage()
        {
            string json = "{\"Devices\": [{\"ip\": \"10.0.0.1\", \"port\": 4370, \"origen\": \"DEV\"}]}";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*nombre*");
        }

        // ─── Invalid field values ─────────────────────────────────────────────

        [Fact]
        public void Given_PortIsNonIntegerString_When_LoadFromJson_Then_ThrowsWithPortInMessage()
        {
            string json = "{\"Devices\": [{\"ip\": \"10.0.0.1\", \"port\": \"abc\", \"origen\": \"DEV\", \"nombre\": \"D1\"}]}";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*port*");
        }

        // ─── Optional fields ──────────────────────────────────────────────────

        [Fact]
        public void Given_PasswordAbsent_When_LoadFromJson_Then_PasswordIsEmptyString()
        {
            string json = BuildJson(password: null);  // omit password field

            Device[] devices = JsonConfigLoader.LoadFromJson(json);

            devices[0].Connection.Password.Should().Be(string.Empty);
        }

        [Fact]
        public void Given_BorrarMarcasAbsent_When_LoadFromJson_Then_BorrarMarcasIsFalse()
        {
            string json = BuildJson(borrarMarcas: null);  // omit borrarMarcas field

            Device[] devices = JsonConfigLoader.LoadFromJson(json);

            devices[0].BorrarMarcas.Should().BeFalse();
        }

        [Fact]
        public void Given_BorrarMarcasTrue_When_LoadFromJson_Then_BorrarMarcasIsTrue()
        {
            string json = BuildJson(borrarMarcas: true);

            Device[] devices = JsonConfigLoader.LoadFromJson(json);

            devices[0].BorrarMarcas.Should().BeTrue();
        }

        // ─── Malformed / edge-case input ─────────────────────────────────────

        [Fact]
        public void Given_MalformedJson_When_LoadFromJson_Then_ThrowsInvalidOperationException()
        {
            string json = "{bad json";

            Action act = () => JsonConfigLoader.LoadFromJson(json);

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Given_EmptyString_When_LoadFromJson_Then_Throws()
        {
            Action act = () => JsonConfigLoader.LoadFromJson(string.Empty);

            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Given_NullInput_When_LoadFromJson_Then_Throws()
        {
            Action act = () => JsonConfigLoader.LoadFromJson(null);

            act.Should().Throw<Exception>();
        }
    }
}
