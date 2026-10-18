using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Annotations;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Graph.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Scanning;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;
using ApiFixtures = PublicApiSurfaceSelectorTestFixtures;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CanonicalAnnotationFamilyResolutionTests
{
    [TestCase("CommandAttribute", "Command")]
    [TestCase("CommandHandlerAttribute", "CommandHandler")]
    [TestCase("QueryAttribute", "Query")]
    [TestCase("QueryHandlerAttribute", "QueryHandler")]
    [TestCase("EventAttribute", "Event")]
    [TestCase("EventHandlerAttribute", "EventHandler")]
    [TestCase("IntegrationEventAttribute", "IntegrationEvent")]
    [TestCase("ReadModelAttribute", "ReadModel")]
    [TestCase("ControllerAttribute", "Controller")]
    [TestCase("EndpointAttribute", "Endpoint")]
    [TestCase("RequestDtoAttribute", "RequestDto")]
    [TestCase("ResponseDtoAttribute", "ResponseDto")]
    [TestCase("ApiContractAttribute", "ApiContract")]
    [TestCase("ValidatorAttribute", "Validator")]
    [TestCase("PortAttribute", "Port")]
    [TestCase("AdapterAttribute", "Adapter")]
    [TestCase("AntiCorruptionLayerAttribute", "AntiCorruptionLayer")]
    [TestCase("DbContextAttribute", "DbContext")]
    [TestCase("RepositoryImplementationAttribute", "RepositoryImplementation")]
    [TestCase("ExternalClientAttribute", "ExternalClient")]
    [TestCase("MessageBusAdapterAttribute", "MessageBusAdapter")]
    [TestCase("FileSystemAdapterAttribute", "FileSystemAdapter")]
    [TestCase("ClockAdapterAttribute", "ClockAdapter")]
    [TestCase("ViewAttribute", "View")]
    [TestCase("ViewModelAttribute", "ViewModel")]
    [TestCase("PresenterAttribute", "Presenter")]
    [TestCase("MonoBehaviourAdapterAttribute", "MonoBehaviourAdapter")]
    [TestCase("ScriptableObjectAssetAttribute", "ScriptableObjectAsset")]
    [TestCase("InputAdapterAttribute", "InputAdapter")]
    [TestCase("SceneAdapterAttribute", "SceneAdapter")]
    public void RoleIndex_EveryApprovedRole_UsesTheExistingCanonicalCatalog(string attributeTypeName, string expectedRole)
    {
        Type attributeType = typeof(RoleAnnotationAttributeBase).Assembly.GetType(
            $"ArchLinterNet.Annotations.{attributeTypeName}", throwOnError: true)!;
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateMarkedModule(expectedRole);
        TypeBuilder typeBuilder = DefineRoleType(module, $"CanonicalAnnotationFixtures.{expectedRole}Fixture", attributeType);
        Type type = typeBuilder.CreateType()!;
        ArchitectureRoleIndex roleIndex = new(
            new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex([assembly]));
        bool found = roleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult result);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(result.Role, Is.EqualTo(expectedRole));
            Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
            Assert.That(result.Evidence, Is.EqualTo($"ArchLinterNet.Annotations.{attributeTypeName}"));
            Assert.That(result.EvidenceSources, Is.EqualTo(new[] { $"ArchLinterNet.Annotations.{attributeTypeName}" }));
            Assert.That(result.Metadata["boundedContext"], Is.EqualTo("Sales"));
            Assert.That(result.Metadata["module"], Is.EqualTo("Orders"));
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
            Assert.That(roleIndex.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [TestCase("CommandAttribute", "Command")]
    [TestCase("CommandHandlerAttribute", "CommandHandler")]
    [TestCase("QueryAttribute", "Query")]
    [TestCase("QueryHandlerAttribute", "QueryHandler")]
    [TestCase("EventAttribute", "Event")]
    [TestCase("EventHandlerAttribute", "EventHandler")]
    [TestCase("IntegrationEventAttribute", "IntegrationEvent")]
    [TestCase("ReadModelAttribute", "ReadModel")]
    [TestCase("ControllerAttribute", "Controller")]
    [TestCase("EndpointAttribute", "Endpoint")]
    [TestCase("RequestDtoAttribute", "RequestDto")]
    [TestCase("ResponseDtoAttribute", "ResponseDto")]
    [TestCase("ApiContractAttribute", "ApiContract")]
    [TestCase("ValidatorAttribute", "Validator")]
    [TestCase("PortAttribute", "Port")]
    [TestCase("AdapterAttribute", "Adapter")]
    [TestCase("AntiCorruptionLayerAttribute", "AntiCorruptionLayer")]
    [TestCase("DbContextAttribute", "DbContext")]
    [TestCase("RepositoryImplementationAttribute", "RepositoryImplementation")]
    [TestCase("ExternalClientAttribute", "ExternalClient")]
    [TestCase("MessageBusAdapterAttribute", "MessageBusAdapter")]
    [TestCase("FileSystemAdapterAttribute", "FileSystemAdapter")]
    [TestCase("ClockAdapterAttribute", "ClockAdapter")]
    [TestCase("ViewAttribute", "View")]
    [TestCase("ViewModelAttribute", "ViewModel")]
    [TestCase("PresenterAttribute", "Presenter")]
    [TestCase("MonoBehaviourAdapterAttribute", "MonoBehaviourAdapter")]
    [TestCase("ScriptableObjectAssetAttribute", "ScriptableObjectAsset")]
    [TestCase("InputAdapterAttribute", "InputAdapter")]
    [TestCase("SceneAdapterAttribute", "SceneAdapter")]
    public void Extract_EquivalentCustomMapping_ProducesSameRoleAndMetadata(
        string attributeTypeName,
        string expectedRole)
    {
        Type canonicalAttribute = typeof(RoleAnnotationAttributeBase).Assembly.GetType(
            $"ArchLinterNet.Annotations.{attributeTypeName}", throwOnError: true)!;
        Type canonicalType = CreateSingleAnnotatedType($"Canonical{expectedRole}", canonicalAttribute);
        Type mappedType = CreateSingleMappedType($"Mapped{expectedRole}");
        ArchitectureTypeClassificationResult canonical = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration(), [canonicalType]).Extract(canonicalType);
        ArchitectureClassificationConfiguration customConfiguration = new();
        customConfiguration.Attributes.Add(new ArchitectureAttributeClassificationMapping
        {
            Attribute = typeof(DomainMarkerAttribute).FullName!,
            Role = expectedRole,
            Metadata = new Dictionary<string, object>
            {
                ["boundedContext"] = "constructor[0]",
                ["module"] = "constructor[1]"
            }
        });
        ArchitectureTypeClassificationResult custom = new ArchitectureAttributeRoleExtractor(
            customConfiguration, [mappedType]).Extract(mappedType);

        Assert.Multiple(() =>
        {
            Assert.That(canonical.Role, Is.EqualTo(expectedRole));
            Assert.That(custom.Role, Is.EqualTo(expectedRole));
            Assert.That(custom.Metadata, Is.EquivalentTo(canonical.Metadata));
            Assert.That(canonical.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
            Assert.That(custom.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
            Assert.That(canonical.Evidence,
                Is.EqualTo($"ArchLinterNet.Annotations.{attributeTypeName}"));
            Assert.That(custom.Evidence, Is.EqualTo(typeof(DomainMarkerAttribute).FullName));
            Assert.That(canonical.EvidenceSources,
                Is.EqualTo(new[] { $"ArchLinterNet.Annotations.{attributeTypeName}" }));
            Assert.That(custom.EvidenceSources, Is.EqualTo(new[] { typeof(DomainMarkerAttribute).FullName }));
        });
    }

    [Test]
    public void Extract_ConflictingCqrsRolesOnOneType_FailsClosedWithStableEvidence()
    {
        (AssemblyBuilder _, ModuleBuilder module) = CreateMarkedModule("ConflictingCqrsRoles");
        TypeBuilder typeBuilder = DefineRoleType(module, "CanonicalAnnotationFixtures.ConflictingType", typeof(CommandAttribute));
        AddRoleAttribute(typeBuilder, typeof(CommandHandlerAttribute));
        Type type = typeBuilder.CreateType()!;
        ArchitectureTypeClassificationResult result = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration(), [type]).Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.Null);
            ArchitectureCanonicalAnnotationDiagnostic conflict = result.CanonicalAnnotationDiagnostics
                .Single(diagnostic => diagnostic.Code == "CanonicalRoleConflict");
            Assert.That(conflict.EvidenceSources, Is.EqualTo(new[]
            {
                "ArchLinterNet.Annotations.CommandAttribute",
                "ArchLinterNet.Annotations.CommandHandlerAttribute"
            }));
        });
    }

    [Test]
    public void CanonicalCqrsAndApiRoles_FlowThroughSelectorsCoverageAndExplain()
    {
        (Assembly assembly, IReadOnlyDictionary<string, Type> types) = CreateSemanticFixture();
        ArchitectureTypeIndex typeIndex = new([assembly]);
        ArchitectureRoleIndex roleIndex = new(new ArchitectureClassificationConfiguration(), typeIndex);

        foreach ((string role, Type type) in types)
        {
            Assert.That(roleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult fact), Is.True, role);
            Assert.That(fact.Role, Is.EqualTo(role), role);
            Assert.That(fact.Metadata["boundedContext"], Is.EqualTo("Sales"), role);
        }

        ArchitectureContextSelector selector = new()
        {
            Role = "CommandHandler",
            Metadata = new Dictionary<string, object> { ["boundedContext"] = "Sales" }
        };
        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, types["CommandHandler"], roleIndex, null), Is.True);

        ArchitectureContractDocument document = CreateSemanticDocument(assembly);
        ArchitectureCoverageContract coverage = new()
        {
            Name = "canonical-role-coverage",
            Id = "canonical-role-coverage",
            Scope = "semantic_role",
            Roots = { new ArchitectureCoverageRoot { Namespace = "CanonicalAnnotationFixtures" } }
        };
        using ArchitectureAnalysisContext context = CreateContext(assembly);
        ArchitectureContractRunner runner = new(context, document);
        ArchitectureCoverageSummary summary = runner.BuildCoverageSummary(coverage)!;

        Assert.That(summary.CoveredItems.Select(item => item.Item), Does.Contain(types["Command"].FullName));

        ArchitectureDependencyContract dependency = document.Contracts.Strict.Single();
        List<ArchitectureViolation> violations = runner.CheckContract(dependency);
        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Namespace, violations);
        ArchitectureExplainApplicationService explainService = new(new FixedGraphApplicationService(new ArchitectureGraphOutcome(graph)));
        ArchitectureExplainOutcome explain = explainService.Explain(new ArchitectureExplainRequest
        {
            PolicyPath = "unused-by-the-prebuilt-graph-fixture",
            Source = "CanonicalAnnotationFixtures.Handlers",
            Target = "CanonicalAnnotationFixtures.Messages",
            Mode = "strict",
            Level = ArchitectureGraphLevel.Namespace
        });

        Assert.Multiple(() =>
        {
            Assert.That(violations, Is.Not.Empty);
            Assert.That(explain.Path, Is.EqualTo(new[]
            {
                "CanonicalAnnotationFixtures.Handlers",
                "CanonicalAnnotationFixtures.Messages"
            }));
            Assert.That(explain.ContractIds, Does.Contain("handler-to-command"));
        });
    }

    [Test]
    public void ApiContractRole_DoesNotSelectReviewedApiMembershipForValueObject()
    {
        (Assembly assembly, IReadOnlyDictionary<string, Type> types) = CreateSemanticFixture();
        string assemblyName = assembly.GetName().Name!;
        ArchitecturePublicApiSurfaceContract contract = new()
        {
            Name = "marked-api-surface",
            Assemblies = [assemblyName],
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(ApiFixtures.PublicApiContractAttribute).FullName!
            }
        };
        ArchitectureContractDocument document = CreateSemanticDocument(assembly);
        document.Contracts.StrictPublicApiSurface.Add(contract);
        using ArchitectureAnalysisContext context = CreateContext(assembly);
        ArchitectureContractRunner runner = new(context, document);
        HashSet<string> governed = runner.Session.CheckPublicApiSurfaceContract(contract)
            .Select(violation => violation.SourceType)
            .ToHashSet(StringComparer.Ordinal);
        ArchitectureRoleIndex roleIndex = new(new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex([assembly]));

        Assert.Multiple(() =>
        {
            Assert.That(roleIndex.TryGetRole(types["ValueObject"], out ArchitectureTypeClassificationResult valueObject), Is.True);
            Assert.That(valueObject.Role, Is.EqualTo("ValueObject"));
            Assert.That(roleIndex.TryGetRole(types["ApiContract"], out ArchitectureTypeClassificationResult apiContract), Is.True);
            Assert.That(apiContract.Role, Is.EqualTo("ApiContract"));
            Assert.That(governed, Does.Contain(types["ValueObject"].FullName));
            Assert.That(governed, Does.Not.Contain(types["ApiContract"].FullName));
        });
    }

    private static ArchitectureAnalysisContext CreateContext(Assembly assembly) => new(
        "/tmp",
        [assembly],
        Array.Empty<string>(),
        Array.Empty<string>());

    private static ArchitectureContractDocument CreateSemanticDocument(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name!;
        ArchitectureContractDocument document = new()
        {
            Name = "canonical-annotation-consumer",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["commands"] = new() { Selector = new ArchitectureLayerSelector { Role = "Command" } },
                ["handlers"] = new() { Selector = new ArchitectureLayerSelector { Role = "CommandHandler" } }
            }
        };
        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Id = "handler-to-command",
            Name = "handler-to-command",
            Source = "handlers",
            Forbidden = { "commands" }
        });
        return document;
    }

    private static (Assembly Assembly, IReadOnlyDictionary<string, Type> Types) CreateSemanticFixture()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateMarkedModule("CqrsAndApiFixture");
        TypeBuilder command = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Messages.SubmitOrderCommand", typeof(CommandAttribute));
        TypeBuilder commandHandler = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Handlers.SubmitOrderHandler", typeof(CommandHandlerAttribute));
        commandHandler.DefineField("Command", command, FieldAttributes.Public);
        TypeBuilder query = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Queries.FindInventory", typeof(QueryAttribute));
        TypeBuilder readModel = DefineRoleType(
            module, "CanonicalAnnotationFixtures.ReadModels.InventoryReadModel", typeof(ReadModelAttribute));
        TypeBuilder integrationEvent = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Messages.PaymentCaptured", typeof(IntegrationEventAttribute));
        TypeBuilder requestDto = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Api.CreateOrderRequest", typeof(RequestDtoAttribute));
        TypeBuilder responseDto = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Api.OrderResponse", typeof(ResponseDtoAttribute));
        TypeBuilder controller = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Api.OrdersController", typeof(ControllerAttribute));
        TypeBuilder endpoint = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Api.CreateOrderEndpoint", typeof(EndpointAttribute));
        TypeBuilder apiContract = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Api.ApiContractOnly", typeof(ApiContractAttribute));
        TypeBuilder validator = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Api.OrderValidator", typeof(ValidatorAttribute));
        TypeBuilder valueObject = DefineRoleType(
            module, "CanonicalAnnotationFixtures.Api.Money", typeof(ValueObjectAttribute), apiMarker: true);

        Dictionary<string, Type> types = new(StringComparer.Ordinal)
        {
            ["Command"] = command.CreateType()!,
            ["CommandHandler"] = commandHandler.CreateType()!,
            ["Query"] = query.CreateType()!,
            ["ReadModel"] = readModel.CreateType()!,
            ["IntegrationEvent"] = integrationEvent.CreateType()!,
            ["RequestDto"] = requestDto.CreateType()!,
            ["ResponseDto"] = responseDto.CreateType()!,
            ["Controller"] = controller.CreateType()!,
            ["Endpoint"] = endpoint.CreateType()!,
            ["ApiContract"] = apiContract.CreateType()!,
            ["Validator"] = validator.CreateType()!,
            ["ValueObject"] = valueObject.CreateType()!
        };

        return (assembly, types);
    }

    private static Type CreateSingleAnnotatedType(string name, Type attributeType)
    {
        (AssemblyBuilder _, ModuleBuilder module) = CreateMarkedModule(name);
        return DefineRoleType(module, $"CanonicalAnnotationFixtures.{name}", attributeType).CreateType()!;
    }

    private static Type CreateSingleMappedType(string name)
    {
        (AssemblyBuilder _, ModuleBuilder module) = CreateMarkedModule(name);
        TypeBuilder typeBuilder = module.DefineType(
            $"CanonicalAnnotationFixtures.{name}", TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        ConstructorInfo constructor = typeof(DomainMarkerAttribute).GetConstructor([typeof(string), typeof(string)])!;
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, ["Sales", "Orders"]));
        return typeBuilder.CreateType()!;
    }

    private static (AssemblyBuilder Assembly, ModuleBuilder Module) CreateMarkedModule(string name)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CanonicalAnnotationFamily.{name}.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("fixtures");
        ConstructorInfo markerConstructor = typeof(AnnotationCatalogIdentityAttribute)
            .GetConstructor([typeof(int), typeof(string)])!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(markerConstructor, [1, "0.10.0"]));
        return (assembly, module);
    }

    private static TypeBuilder DefineRoleType(
        ModuleBuilder module,
        string fullName,
        Type roleAttributeType,
        string boundedContext = "Sales",
        string moduleName = "Orders",
        bool apiMarker = false)
    {
        TypeBuilder typeBuilder = module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        AddRoleAttribute(typeBuilder, roleAttributeType, boundedContext, moduleName);
        if (apiMarker)
        {
            ConstructorInfo markerConstructor = typeof(ApiFixtures.PublicApiContractAttribute).GetConstructor(Type.EmptyTypes)!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(markerConstructor, Array.Empty<object>()));
        }

        return typeBuilder;
    }

    private static void AddRoleAttribute(
        TypeBuilder typeBuilder,
        Type roleAttributeType,
        string boundedContext = "Sales",
        string moduleName = "Orders")
    {
        ConstructorInfo constructor = roleAttributeType.GetConstructor(Type.EmptyTypes)!;
        PropertyInfo boundedContextProperty = roleAttributeType.GetProperty(nameof(RoleAnnotationAttributeBase.BoundedContext))!;
        PropertyInfo moduleProperty = roleAttributeType.GetProperty(nameof(RoleAnnotationAttributeBase.Module))!;
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            constructor,
            Array.Empty<object>(),
            [boundedContextProperty, moduleProperty],
            [boundedContext, moduleName]));
    }

    private sealed class FixedGraphApplicationService(ArchitectureGraphOutcome outcome) : IArchitectureGraphApplicationService
    {
        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => outcome;
    }
}
