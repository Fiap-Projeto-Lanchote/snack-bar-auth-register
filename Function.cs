using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.Core;
using Snack.Bar.Auth.Register.Models;
using System.Text.RegularExpressions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Snack.Bar.Auth.Register;

public class Function
{
    private static readonly string? _clientId = Environment.GetEnvironmentVariable("COGNITO_CLIENT_ID");
    private static readonly string? _userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID");

    private static readonly AmazonCognitoIdentityProviderClient _provider = new();

    public async Task<UserResponse> FunctionHandler(UserRequest user, ILambdaContext context)
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
                // Usu�rio j� existe: atualiza os atributos
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

                return new UserResponse
                {
                    Success = true,
                    Message = "Usu�rio atualizado com sucesso!"
                };
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

                return new UserResponse
                {
                    Success = true,
                    Message = "Usu�rio criado com sucesso!"
                };
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Erro: {ex.Message}");

            return new UserResponse
            {
                Success = false,
                Message = $"Erro ao processar o usu�rio: {ex.Message}"
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
        
        // Dispara erro caso o campo telefone seja preenchido com um formato invalido pelo cognito
        if (!string.IsNullOrWhiteSpace(request.Phone) && !IsValidPhoneNumber(request.Phone))
            throw new Exception($"Invalid Payload: Phone number '{request.Phone}' is not in the correct E.164 format (+[country_code][number]).");
    }

    /// <summary>
    /// Valida se o n�mero de telefone est� no formato E.164 (+[c�digo do pa�s][n�mero])
    /// Exemplo v�lido: +5511912345678 (Brasil)
    /// </summary>
    private static bool IsValidPhoneNumber(string phone)
    {
        return Regex.IsMatch(phone, @"^\+\d{1,15}$");
    }
}
