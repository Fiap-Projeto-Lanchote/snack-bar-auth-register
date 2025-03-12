using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.Core;
using Snack.Bar.Auth.Register.Models;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Snack.Bar.Auth.Register;

public class Function
{
    private static readonly string? _clientId = Environment.GetEnvironmentVariable("COGNITO_CLIENT_ID");
    private static readonly string? _userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID");

    private static readonly AmazonCognitoIdentityProviderClient _provider = new();

    public async Task<UserResponse> FunctionHandler(UserRequest request, ILambdaContext context)
    {
        try
        {
            ValidatePayload(request);
            ValidateEnvironmentVariables();

            var listUsersRequest = new ListUsersRequest
            {
                UserPoolId = _userPoolId,
                Filter = $"email = \"{request.Email}\""
            };

            var userList = await _provider.ListUsersAsync(listUsersRequest);

            // Cria a lista base de atributos (name, email e opcionalmente telefone)
            var attributes = new List<AttributeType>
        {
            new AttributeType { Name = "name", Value = request.Name },
            new AttributeType { Name = "email", Value = request.Email }
        };

            if (!string.IsNullOrEmpty(request.Phone))
            {
                attributes.Add(new AttributeType { Name = "phone_number", Value = request.Phone });
            }

            if (userList.Users.Count > 0)
            {
                // Usuário já existe: atualiza os atributos
                var updateRequest = new AdminUpdateUserAttributesRequest
                {
                    UserPoolId = _userPoolId,
                    Username = request.Email,
                    UserAttributes = attributes
                };

                await _provider.AdminUpdateUserAttributesAsync(updateRequest);

                // Opcional: se desejar atualizar a senha também, pode chamar AdminSetUserPassword
                var setPasswordRequest = new AdminSetUserPasswordRequest
                {
                    UserPoolId = _userPoolId,
                    Username = request.Email,
                    Password = request.Password,
                    Permanent = true
                };

                await _provider.AdminSetUserPasswordAsync(setPasswordRequest);

                return new UserResponse
                {
                    Success = true,
                    Message = "Usuário atualizado com sucesso!"
                };
            }
            else
            {
                // Usuário não existe: cria o usuário usando a senha informada como permanente
                var createUserRequest = new AdminCreateUserRequest
                {
                    UserPoolId = _userPoolId,
                    Username = request.Email,
                    UserAttributes = attributes,
                    TemporaryPassword = request.Password, // Senha inicial (temporária)
                    MessageAction = MessageActionType.SUPPRESS  // Suprime o envio de e-mail
                };

                await _provider.AdminCreateUserAsync(createUserRequest);

                // Imediatamente define a senha como permanente
                var setPasswordRequest = new AdminSetUserPasswordRequest
                {
                    UserPoolId = _userPoolId,
                    Username = request.Email,
                    Password = request.Password,
                    Permanent = true
                };

                await _provider.AdminSetUserPasswordAsync(setPasswordRequest);

                return new UserResponse
                {
                    Success = true,
                    Message = "Usuário criado com sucesso!"
                };
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Erro: {ex.Message}");
            return new UserResponse
            {
                Success = false,
                Message = $"Erro ao processar o usuário: {ex.Message}"
            };
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
}
