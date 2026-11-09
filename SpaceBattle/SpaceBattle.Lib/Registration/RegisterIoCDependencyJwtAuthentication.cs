using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using App;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyJwtAuthentication(string issuer, string audience, string secret) : ICommand
{
    public void Execute()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        var key = Encoding.UTF8.GetBytes(secret);
        if (key.Length < 32) throw new ArgumentException("The signing key must contain at least 32 UTF-8 bytes.");
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        var validator = new JwtPlayerTokenValidator(new JsonWebTokenHandler { MapInboundClaims = false }, parameters,
            claims => new PlayerIdentity(Required(claims, "sub"), Required(claims, "game"), claims.HasClaim("permission", "transfer")));
        IoCRegistrar.Register(AuthenticationDependencies.Validator, _ => validator);
        IoCRegistrar.Register(AuthenticationDependencies.Authenticate, args =>
        {
            if (args is not [IMessage message, PlayerIdentity player]) throw new ArgumentException("Expected message and authenticated player.");
            if (message.GameId != player.GameId) throw new UnauthorizedAccessException("The token belongs to another game.");
            return new AuthenticatedMessage(new Message(message.GameId, message.Command,
                message.Args.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement<object?>(null) : message.Args), player.PlayerId);
        });
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        var interpreter = new DelegatingMessageInterpreter(() => ScopeExecution.InScope(scope,
            () => Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Interpreter)));
        var serializer = new AuthenticatedCommandSerializer(interpreter,
            message => new JsonObject
            {
                ["gameId"] = message.GameId,
                ["playerId"] = message.PlayerId,
                ["command"] = message.Command,
                ["args"] = JsonNode.Parse(message.Args.GetRawText())
            }, node => new AuthenticatedMessage(new Message(node["gameId"]!.GetValue<string>(),
                node["command"]!.GetValue<string>(), JsonSerializer.SerializeToElement(node["args"])), node["playerId"]!.GetValue<string>()),
            (inner, message) => new InterpretMessageCommand(inner, message));
        Ioc.Resolve<List<ICommandSerializer>>(SerializationDependencies.CommandSerializers).Insert(0, serializer);
    }

    private static string Required(ClaimsIdentity identity, string type)
    {
        var claims = identity.FindAll(type).ToArray();
        return claims.Length == 1 && !string.IsNullOrWhiteSpace(claims[0].Value) ? claims[0].Value
            : throw new UnauthorizedAccessException($"The token must contain one '{type}' claim.");
    }
}
