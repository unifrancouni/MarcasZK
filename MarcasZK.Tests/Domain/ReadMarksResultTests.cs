using System;
using System.Linq;
using FluentAssertions;
using MarcasZK.Domain;
using Xunit;

namespace MarcasZK.Tests.Domain
{
    public class ReadMarksResultTests
    {
        [Fact]
        public void Given_NewReadMarksResult_When_Constructed_Then_SuccessIsFalse()
        {
            var result = new ReadMarksResult();

            result.Success.Should().BeFalse();
        }

        [Fact]
        public void Given_NewReadMarksResult_When_Constructed_Then_MarksIsEmptyList()
        {
            var result = new ReadMarksResult();

            result.Marks.Should().NotBeNull();
            result.Marks.Should().BeEmpty();
        }

        [Fact]
        public void Given_NewReadMarksResult_When_Constructed_Then_ErrorTypeIsNone()
        {
            var result = new ReadMarksResult();

            result.ErrorType.Should().Be(ReadErrorType.None);
        }

        [Fact]
        public void Given_ReadErrorType_When_Enumerated_Then_ContainsExactlySixValues()
        {
            var values = Enum.GetValues(typeof(ReadErrorType)).Cast<ReadErrorType>().ToArray();

            values.Should().HaveCount(6);
            values.Should().Contain(ReadErrorType.None);
            values.Should().Contain(ReadErrorType.Connection);
            values.Should().Contain(ReadErrorType.ReadData);
            values.Should().Contain(ReadErrorType.NoData);
            values.Should().Contain(ReadErrorType.ComNotRegistered);
            values.Should().Contain(ReadErrorType.Unexpected);
        }
    }
}
