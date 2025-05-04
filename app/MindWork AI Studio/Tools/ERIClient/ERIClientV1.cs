using System.Text;
using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ERIClient;

public class ERIClientV1(IERIDataSource dataSource) : ERIClientBase(dataSource), IERIClient
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ERIClientV1).Namespace, nameof(ERIClientV1));
    
    #region Implementation of IERIClient

    public async Task<APIResponse<List<AuthScheme>>> GetAuthMethodsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await this.httpClient.GetAsync("/auth/methods", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new()
                {
                    Successful = false,
                    Message = string.Format(TB("Failed to retrieve the authentication methods: there was an issue communicating with the ERI server. Code: {0}, Reason: {1}"), response.StatusCode, response.ReasonPhrase)
                };
            }

            var authMethods = await response.Content.ReadFromJsonAsync<List<AuthScheme>>(JSON_OPTIONS, cancellationToken);
            if (authMethods is null)
            {
                return new()
                {
                    Successful = false,
                    Message = TB("Failed to retrieve the authentication methods: the ERI server did not return a valid response.")
                };
            }

            return new()
            {
                Successful = true,
                Data = authMethods
            };
        }
        catch (TaskCanceledException)
        {
            return new()
            {
                Successful = false,
                Message = TB("Failed to retrieve the authentication methods: the request was canceled either by the user or due to a timeout.")
            };
        }
        catch (Exception e)
        {
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to retrieve the authentication methods due to an exception: {0}"), e.Message)
            };
        }
    }

    public async Task<APIResponse<AuthResponse>> AuthenticateAsync(RustService rustService, string? temporarySecret = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var authMethod = this.dataSource.AuthMethod;
            var username = this.dataSource.Username;
            switch (this.dataSource.AuthMethod)
            {
                case AuthMethod.NONE:
                    using (var request = new HttpRequestMessage(HttpMethod.Post, $"auth?authMethod={authMethod}"))
                    {
                        using var noneAuthResponse = await this.httpClient.SendAsync(request, cancellationToken);
                        if(!noneAuthResponse.IsSuccessStatusCode)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = string.Format(TB("Failed to authenticate with the ERI server. Code: {0}, Reason: {1}"), noneAuthResponse.StatusCode, noneAuthResponse.ReasonPhrase)
                            };
                        }
                    
                        var noneAuthResult = await noneAuthResponse.Content.ReadFromJsonAsync<AuthResponse>(JSON_OPTIONS, cancellationToken);
                        if(noneAuthResult == default)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = TB("Failed to authenticate with the ERI server: the response was invalid.")
                            };
                        }
                    
                        this.securityToken = noneAuthResult.Token ?? string.Empty;
                        return new()
                        {
                            Successful = true,
                            Data = noneAuthResult
                        };
                    }
            
                case AuthMethod.USERNAME_PASSWORD:
                    string password;
                    if (string.IsNullOrWhiteSpace(temporarySecret))
                    {
                        var passwordResponse = await rustService.GetSecret(this.dataSource);
                        if (!passwordResponse.Success)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = TB("Failed to retrieve the password.")
                            };
                        }

                        password = await passwordResponse.Secret.Decrypt(Program.ENCRYPTION);
                    }
                    else
                        password = temporarySecret;

                    using (var request = new HttpRequestMessage(HttpMethod.Post, $"auth?authMethod={authMethod}"))
                    {
                        // We must send both values inside the header. The username field is named 'user'.
                        // The password field is named 'password'.
                        request.Headers.Add("user", username);
                        request.Headers.Add("password", password);

                        using var usernamePasswordAuthResponse = await this.httpClient.SendAsync(request, cancellationToken);
                        if(!usernamePasswordAuthResponse.IsSuccessStatusCode)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = string.Format(TB("Failed to authenticate with the ERI server. Code: {0}, Reason: {1}"), usernamePasswordAuthResponse.StatusCode, usernamePasswordAuthResponse.ReasonPhrase)
                            };
                        }
                    
                        var usernamePasswordAuthResult = await usernamePasswordAuthResponse.Content.ReadFromJsonAsync<AuthResponse>(JSON_OPTIONS, cancellationToken);
                        if(usernamePasswordAuthResult == default)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = TB("Failed to authenticate with the server: the response was invalid.")
                            };
                        }
                    
                        this.securityToken = usernamePasswordAuthResult.Token ?? string.Empty;
                        return new()
                        {
                            Successful = true,
                            Data = usernamePasswordAuthResult
                        };
                    }

                case AuthMethod.TOKEN:
                    string token;
                    if (string.IsNullOrWhiteSpace(temporarySecret))
                    {
                        var tokenResponse = await rustService.GetSecret(this.dataSource);
                        if (!tokenResponse.Success)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = TB("Failed to retrieve the access token.")
                            };
                        }

                        token = await tokenResponse.Secret.Decrypt(Program.ENCRYPTION);
                    }
                    else
                        token = temporarySecret;

                    using (var request = new HttpRequestMessage(HttpMethod.Post, $"auth?authMethod={authMethod}"))
                    {
                        request.Headers.Add("Authorization", $"Bearer {token}");
                    
                        using var tokenAuthResponse = await this.httpClient.SendAsync(request, cancellationToken);
                        if(!tokenAuthResponse.IsSuccessStatusCode)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = string.Format(TB("Failed to authenticate with the ERI server. Code: {0}, Reason: {1}"), tokenAuthResponse.StatusCode, tokenAuthResponse.ReasonPhrase)
                            };
                        }
                    
                        var tokenAuthResult = await tokenAuthResponse.Content.ReadFromJsonAsync<AuthResponse>(JSON_OPTIONS, cancellationToken);
                        if(tokenAuthResult == default)
                        {
                            return new()
                            {
                                Successful = false,
                                Message = TB("Failed to authenticate with the ERI server: the response was invalid.")
                            };
                        }
                    
                        this.securityToken = tokenAuthResult.Token ?? string.Empty;
                        return new()
                        {
                            Successful = true,
                            Data = tokenAuthResult
                        };
                    }
                
                default:
                    this.securityToken = string.Empty;
                    return new()
                    {
                        Successful = false,
                        Message = TB("The authentication method is not supported yet.")
                    };
            }
        }
        catch(TaskCanceledException)
        {
            return new()
            {
                Successful = false,
                Message = TB("Failed to authenticate with the ERI server: the request was canceled either by the user or due to a timeout.")
            };
        }
        catch (Exception e)
        {
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to authenticate with the ERI server due to an exception: {0}"), e.Message)
            };
        }
    }

    public async Task<APIResponse<DataSourceInfo>> GetDataSourceInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/dataSource");
            request.Headers.Add("token", this.securityToken);
        
            using var response = await this.httpClient.SendAsync(request, cancellationToken);
            if(!response.IsSuccessStatusCode)
            {
                return new()
                {
                    Successful = false,
                    Message = string.Format(TB("Failed to retrieve the data source information: there was an issue communicating with the ERI server. Code: {0}, Reason: {1}"), response.StatusCode, response.ReasonPhrase)
                };
            }
        
            var dataSourceInfo = await response.Content.ReadFromJsonAsync<DataSourceInfo>(JSON_OPTIONS, cancellationToken);
            if(dataSourceInfo == default)
            {
                return new()
                {
                    Successful = false,
                    Message = TB("Failed to retrieve the data source information: the ERI server did not return a valid response.")
                };
            }
        
            return new()
            {
                Successful = true,
                Data = dataSourceInfo
            };
        }
        catch(TaskCanceledException)
        {
            return new()
            {
                Successful = false,
                Message = TB("Failed to retrieve the data source information: the request was canceled either by the user or due to a timeout.")
            };
        }
        catch (Exception e)
        {
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to retrieve the data source information due to an exception: {0}"), e.Message)
            };
        }
    }

    public async Task<APIResponse<List<EmbeddingInfo>>> GetEmbeddingInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/embedding/info");
            request.Headers.Add("token", this.securityToken);
        
            using var response = await this.httpClient.SendAsync(request, cancellationToken);
            if(!response.IsSuccessStatusCode)
            {
                return new()
                {
                    Successful = false,
                    Message = string.Format(TB("Failed to retrieve the embedding information: there was an issue communicating with the ERI server. Code: {0}, Reason: {1}"), response.StatusCode, response.ReasonPhrase)
                };
            }
        
            var embeddingInfo = await response.Content.ReadFromJsonAsync<List<EmbeddingInfo>>(JSON_OPTIONS, cancellationToken);
            if(embeddingInfo is null)
            {
                return new()
                {
                    Successful = false,
                    Message = TB("Failed to retrieve the embedding information: the ERI server did not return a valid response.")
                };
            }
        
            return new()
            {
                Successful = true,
                Data = embeddingInfo
            };
        }
        catch(TaskCanceledException)
        {
            return new()
            {
                Successful = false,
                Message = TB("Failed to retrieve the embedding information: the request was canceled either by the user or due to a timeout.")
            };
        }
        catch (Exception e)
        {
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to retrieve the embedding information due to an exception: {0}"), e.Message)
            };
        }
    }

    public async Task<APIResponse<List<RetrievalInfo>>> GetRetrievalInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/retrieval/info");
            request.Headers.Add("token", this.securityToken);
        
            using var response = await this.httpClient.SendAsync(request, cancellationToken);
            if(!response.IsSuccessStatusCode)
            {
                return new()
                {
                    Successful = false,
                    Message = string.Format(TB("Failed to retrieve the retrieval information: there was an issue communicating with the ERI server. Code: {0}, Reason: {1}"), response.StatusCode, response.ReasonPhrase)
                };
            }
        
            var retrievalInfo = await response.Content.ReadFromJsonAsync<List<RetrievalInfo>>(JSON_OPTIONS, cancellationToken);
            if(retrievalInfo is null)
            {
                return new()
                {
                    Successful = false,
                    Message = TB("Failed to retrieve the retrieval information: the ERI server did not return a valid response.")
                };
            }
        
            return new()
            {
                Successful = true,
                Data = retrievalInfo
            };
        }
        catch(TaskCanceledException)
        {
            return new()
            {
                Successful = false,
                Message = TB("Failed to retrieve the retrieval information: the request was canceled either by the user or due to a timeout.")
            };
        }
        catch (Exception e)
        {
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to retrieve the retrieval information due to an exception: {0}"), e.Message)
            };
        }
    }

    public async Task<APIResponse<List<Context>>> ExecuteRetrievalAsync(RetrievalRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/retrieval");
            requestMessage.Headers.Add("token", this.securityToken);

            using var content = new StringContent(JsonSerializer.Serialize(request, JSON_OPTIONS), Encoding.UTF8, "application/json");
            requestMessage.Content = content;
        
            using var response = await this.httpClient.SendAsync(requestMessage, cancellationToken);
            if(!response.IsSuccessStatusCode)
            {
                return new()
                {
                    Successful = false,
                    Message = string.Format(TB("Failed to execute the retrieval request: there was an issue communicating with the ERI server. Code: {0}, Reason: {1}"), response.StatusCode, response.ReasonPhrase)
                };
            }
        
            var contexts = await response.Content.ReadFromJsonAsync<List<Context>>(JSON_OPTIONS, cancellationToken);
            if(contexts is null)
            {
                return new()
                {
                    Successful = false,
                    Message = TB("Failed to execute the retrieval request: the ERI server did not return a valid response.")
                };
            }
        
            return new()
            {
                Successful = true,
                Data = contexts
            };
        }
        catch(TaskCanceledException)
        {
            return new()
            {
                Successful = false,
                Message = TB("Failed to execute the retrieval request: the request was canceled either by the user or due to a timeout.")
            };
        }
        catch (Exception e)
        {
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to execute the retrieval request due to an exception: {0}"), e.Message)
            };
        }
    }

    public async Task<APIResponse<SecurityRequirements>> GetSecurityRequirementsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/security/requirements");
            request.Headers.Add("token", this.securityToken);
        
            using var response = await this.httpClient.SendAsync(request, cancellationToken);
            if(!response.IsSuccessStatusCode)
            {
                return new()
                {
                    Successful = false,
                    Message = string.Format(TB("Failed to retrieve the security requirements: there was an issue communicating with the ERI server. Code: {0}, Reason: {1}"), response.StatusCode, response.ReasonPhrase)
                };
            }
        
            var securityRequirements = await response.Content.ReadFromJsonAsync<SecurityRequirements>(JSON_OPTIONS, cancellationToken);
            if(securityRequirements == default)
            {
                return new()
                {
                    Successful = false,
                    Message = TB("Failed to retrieve the security requirements: the ERI server did not return a valid response.")
                };
            }
        
            return new()
            {
                Successful = true,
                Data = securityRequirements
            };
        }
        catch(TaskCanceledException)
        {
            return new()
            {
                Successful = false,
                Message = TB("Failed to retrieve the security requirements: the request was canceled either by the user or due to a timeout.")
            };
        }
        catch (Exception e)
        {
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to retrieve the security requirements due to an exception: {0}"), e.Message)
            };
        }
    }

    #endregion
}