// Test doubles compose the public execution seams repeatedly; keep their model and delegate
// imports centralized so each fake declares only the behavior it exercises.
global using ArchLinterNet.Core.Contracts.PolicyImports.Models;
global using ArchLinterNet.Core.Execution.Contracts;
global using ArchLinterNet.Core.Execution.Results;
