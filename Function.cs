using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Snack.Bar.Auth.Register.Models;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Snack.Bar.Auth.Register;

public class Function
{
    private static readonly string? _clientId = Environment.GetEnvironmentVariable("COGNITO_CLIENT_ID");
    private static readonly string? _userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID");

    private static readonly AmazonCognitoIdentityProviderClient _provider = new();

    public async Task<APIGatewayProxyResponse> FunctionHandler(UserRequest user, ILambdaContext context)
    {
        try
        {
            ValidatePayload(user);
            ValidateEnvironmentVariables();

            var listUsersRequest = new ListUsersRequest
            {
                UserPoolId = _userPoolId,
                Filter = $"email = \"{user.Email}\""
            };

            var userList = await _provider.ListUsersAsync(listUsersRequest);

            var attributes = new List<AttributeType>
            {
                new AttributeType { Name = "name", Value = user.Name },
                new AttributeType { Name = "email", Value = user.Email }
            };

            if (!string.IsNullOrEmpty(user.Phone))
            {
                attributes.Add(new AttributeType { Name = "phone_number", Value = user.Phone });
            }

            if (userList.Users.Count > 0)
            {
                // Usuário já existe: atualiza os atributos
                var updateRequest = new AdminUpdateUserAttributesRequest
                {
                    UserPoolId = _userPoolId,
                    Username = user.Name,
                    UserAttributes = attributes
                };

                await _provider.AdminUpdateUserAttributesAsync(updateRequest);

                var setPasswordRequest = new AdminSetUserPasswordRequest
                {
                    UserPoolId = _userPoolId,
                    Username = user.Name,
                    Password = user.Password,
                    Permanent = true
                };

                await _provider.AdminSetUserPasswordAsync(setPasswordRequest);

                return MapToApiGatewayResponse(new UserResponse
                {
                    Success = true,
                    Message = "Usuário atualizado com sucesso!"
                }, 200);
            }
            else
            {
                var createUserRequest = new AdminCreateUserRequest
                {
                    UserPoolId = _userPoolId,
                    Username = user.Name,
                    UserAttributes = attributes,
                    TemporaryPassword = user.Password,
                    MessageAction = MessageActionType.SUPPRESS
                };

                await _provider.AdminCreateUserAsync(createUserRequest);

                var setPasswordRequest = new AdminSetUserPasswordRequest
                {
                    UserPoolId = _userPoolId,
                    Username = user.Name,
                    Password = user.Password,
                    Permanent = true
                };

                await _provider.AdminSetUserPasswordAsync(setPasswordRequest);

                return MapToApiGatewayResponse(new UserResponse
                {
                    Success = true,
                    Message = "Usuário criado com sucesso!"
                }, 201);
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Erro: {ex.Message}");

            return MapToApiGatewayResponse(new UserResponse
            {
                Success = false,
                Message = $"Erro ao processar o usuário: {ex.Message}"
            }, 500);
        }
    }

    private static void ValidateEnvironmentVariables()
    {
        if (string.IsNullOrWhiteSpace(_clientId))
            throw new Exception("Environment Variable COGNITO_CLIENT_ID not found.");

        if (string.IsNullOrWhiteSpace(_userPoolId))
            throw new Exception("Environment Variable COGNITO_USER_POOL_ID not found.");
    }

    private static void ValidatePayload(UserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Email))
            throw new Exception($"Invalid Payload: {request}.");

        if (string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Password))
            throw new Exception($"Invalid Payload: {request}.");

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Name))
            throw new Exception($"Invalid Payload: {request}.");
    }

    private APIGatewayProxyResponse MapToApiGatewayResponse(UserResponse response, int statusCode)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Body = JsonSerializer.Serialize(response),
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            }
        };
    }
}
