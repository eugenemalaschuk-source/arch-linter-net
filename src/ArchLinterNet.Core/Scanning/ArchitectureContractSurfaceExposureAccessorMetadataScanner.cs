using System.Globalization;
using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

// Accessor methods are excluded from the ordinary method loop so their evidence remains attached
// to the owning property/event. Keeping this traversal apart makes that boundary explicit.
internal static partial class ArchitectureContractSurfaceExposureScanner
{
    private sealed partial class Walker
    {
        private void ScanAccessorMetadata(
            MethodInfo accessor,
            ArchitectureContractExposurePath memberPath,
            string accessorKind)
        {
            ArchitectureContractExposurePath accessorPath = memberPath.Append("accessor", accessorKind);
            ScanAttributes(accessor, accessorPath);
            ScanParameters(accessor, accessorPath);
            ScanReturn(accessor, accessorPath);
        }

        private void ScanParameters(MethodBase method, ArchitectureContractExposurePath memberPath)
        {
            ParameterInfo[] parameters = TryReadArray(
                () => method.GetParameters(), memberPath.Append("parameter"), "parameters-unavailable");
            ScanParameters(parameters, memberPath);
        }

        private void ScanParameters(IEnumerable<ParameterInfo> parameters, ArchitectureContractExposurePath memberPath)
        {
            int index = 0;
            foreach (ParameterInfo parameter in parameters)
            {
                ArchitectureContractExposurePath parameterPath = memberPath.Append(
                    "parameter", index.ToString(CultureInfo.InvariantCulture));
                ScanAttributes(parameter, parameterPath);
                Type? parameterType = TryRead(() => parameter.ParameterType, parameterPath, "parameter-type-unavailable");
                if (parameterType != null)
                {
                    ScanShape(parameterType, parameterPath);
                }

                index++;
            }
        }

        private void ScanReturn(MethodInfo method, ArchitectureContractExposurePath memberPath)
        {
            ArchitectureContractExposurePath returnPath = memberPath.Append("return");
            ParameterInfo? returnParameter = TryRead(
                () => method.ReturnParameter, returnPath, "return-parameter-unavailable");
            if (returnParameter == null)
            {
                return;
            }

            ScanAttributes(returnParameter, returnPath);
            Type? returnType = TryRead(() => method.ReturnType, returnPath, "return-type-unavailable");
            if (returnType != null)
            {
                ScanShape(returnType, returnPath);
            }
        }
    }
}
