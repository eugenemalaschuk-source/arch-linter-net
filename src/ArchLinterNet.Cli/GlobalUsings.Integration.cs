// Explicit shared CLI integration boundary. Commands may consume its stable output contract, but
// command behaviour itself stays inside each direct Commands.<Feature> module.
global using ArchLinterNet.Cli.Integration.OutputFormatting;
