﻿using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Catalyst.Common;
using Catalyst.Init;
using Catalyst.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Initialize Local Configuration File
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

// Initialize Azure KeyVault
var secretClient = new SecretClient(new Uri($"https://{config.GetRequiredSection("KeyVault")["KeyVaultName"]}.vault.azure.net"), new ClientSecretCredential(config.GetRequiredSection("KeyVault")["AzureADTennantId"], config.GetRequiredSection("KeyVault")["AzureADClientId"], config.GetRequiredSection("KeyVault")["AzureADClientSecret"]));
await Logger.Log(LogSeverity.Debug, "SecretClientConfigured", $"Configured Azure Key Vault client to connect to {secretClient.VaultUri}.");

var token = secretClient.GetSecret(config.GetRequiredSection("KeyVault")["SecretName"]);
await Logger.Log(LogSeverity.Debug, "AuthTokenObtained", $"Successfully obtained token from Azure Key Vault. Secret ID: {token.Value.Id}");

var client = new DiscordShardedClient();

var commands = new CommandService(new CommandServiceConfig
{
    // Again, log level:
    LogLevel = LogSeverity.Info,

    // There's a few more properties you can set,
    // for example, case-insensitive commands.
    CaseSensitiveCommands = false,
});

// Setup your DI container.
Bootstrapper.Init();
Bootstrapper.RegisterInstance(client);
Bootstrapper.RegisterInstance(commands);
Bootstrapper.RegisterType<ICommandHandler, CommandHandler>();
Bootstrapper.RegisterInstance(config);

await MainAsync();

async Task MainAsync()
{
    await Bootstrapper.ServiceProvider.GetRequiredService<ICommandHandler>().InitializeAsync();

    client.ShardReady += async shard =>
    {
        await Logger.Log(LogSeverity.Info, "ShardReady", $"Shard Number {shard.ShardId} is connected and ready!");
    };

    // Login and connect.    
    if (string.IsNullOrWhiteSpace(token.Value.Value))
    {
        await Logger.Log(LogSeverity.Error, $"{nameof(Program)} | {nameof(MainAsync)}", "Token is null or empty.");
        return;
    }

    await client.LoginAsync(TokenType.Bot, token.Value.Value);
    await Logger.Log(LogSeverity.Debug, $"Client{client.LoginState}", $"Discord Presence: {client.Status}.");
    
    await client.StartAsync();
    await Logger.Log(LogSeverity.Debug, $"ClientReady", $"Client is connected to Discord.");

    // Wait infinitely so your bot actually stays connected.
    await Task.Delay(Timeout.Infinite);
}
