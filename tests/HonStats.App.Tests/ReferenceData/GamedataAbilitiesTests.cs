using System.Net;
using AwesomeAssertions;
using HonStats.Infra.Juvio;
using HonStats.Infra.ReferenceData;
using Xunit;

namespace HonStats.App.Tests.ReferenceData;

public class GamedataAbilitiesTests
{
    private const string AbilitiesJson = """
        {
          "abilities": [
            { "id": 1011, "name": "Ability_Pyromancer2", "translatedName": "Hammer Throw", "icon": ["https://cdn/1011.webp"], "manaCost": [90, 100, 110], "cooldownTime": [8000, 7000, 6000], "range": [600], "targetRadius": [200] },
            { "id": 328, "name": "Ability_AttributeBoost", "translatedName": "Attribute Boost", "icon": ["https://cdn/328.webp"] }
          ]
        }
        """;

    private const string StringsJson = """
        { "language": "en", "strings": { "Ability_Pyromancer2_description": "Throws a hammer." } }
        """;

    [Fact]
    public async Task LoadsAbilitiesKeyedByNumericId_IncludingAttributeBoost()
    {
        var client = new GamedataClient(new FakeGamedataHttpClientFactory());

        var abilities = await client.GetAbilitiesAsync(CancellationToken.None);

        abilities.Should().HaveCount(2);
        abilities.Should().Contain(a => a.Id == 328);

        var hammer = abilities.Single(a => a.Id == 1011);
        hammer.Name.Should().Be("Ability_Pyromancer2");
        hammer.TranslatedName.Should().Be("Hammer Throw");
        hammer.IconUrl.Should().Be("https://cdn/1011.webp");
        hammer.Description.Should().Be("Throws a hammer.");
        hammer.ManaCost.Should().Equal(90, 100, 110);
        hammer.Cooldown.Should().Equal(8, 7, 6);
        hammer.Range.Should().Be(600);
        hammer.TargetRadius.Should().Be(200);

        var boost = abilities.Single(a => a.Id == 328);
        boost.TranslatedName.Should().Be("Attribute Boost");
    }

    private sealed class FakeGamedataHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            name.Should().Be(JuvioHttpClients.GameData);
            return new HttpClient(new RoutingHandler())
            {
                BaseAddress = new Uri("https://gamedata.juvio.com"),
            };
        }
    }

    private sealed class RoutingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path switch
            {
                "/entities/abilities" => AbilitiesJson,
                "/strings" => StringsJson,
                _ => "{}",
            };
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            );
        }
    }
}
