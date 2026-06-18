using ArchLinterNet.Core;
using Unity;



var policyPath = args.ElementAtOrDefault(0) ?? "architecture/dependencies.arch.yml";
var validator = ServiceLocator.Container.Resolve<IArchitectureValidator>();
var result = validator.Validate(policyPath);

Console.WriteLine(result ? "Validation passed." : "Validation failed.");
return result ? 0 : 1;
