using Microsoft.Extensions.Options;
using Notes.Server;
using NotesServer.Notes;
using NSubstitute;

namespace NotesServer.UnitTests
{
    public class BasicAuthServiceTests
    {
        private readonly BasicAuthService _sut;
        private readonly INotesEnvironmentService _environment = 
            Substitute.For<INotesEnvironmentService>();

        public BasicAuthServiceTests()
        {
            _sut = new BasicAuthService(
                new OptionsWrapper<BasicAuthOptions>(new BasicAuthOptions()),
                _environment);
        }

        [Fact]
        public void GetUser_ShouldReturnUser_WhenUserExists()
        {
            // Arrange
            var testUser1 = new User("test1", "secret");
            var testUser2 = new User("test2", "psst");
            var testUser3 = new User("test3", "huh?");
            _environment.Users.Returns([testUser1, testUser2]);

            // Act
            var result_positive = _sut.GetUser(testUser1.ToAuthHeader());
            var result_negative = _sut.GetUser(testUser3.ToAuthHeader());

            // Assert
            Assert.Equal(testUser1, result_positive);
            Assert.Null(result_negative);
        }
    }
}