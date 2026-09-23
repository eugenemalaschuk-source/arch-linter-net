namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal static class ArchLinterNetBadgeLogo
{
    // Compact vector mark for Shields endpoint payloads. It keeps the recognizable
    // ArchLinterNet graph/check motif at badge scale without an external image URL.
    internal const string Svg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect width="32" height="32" rx="7" fill="#3549c8"/>
          <g fill="none" stroke="#fff" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.9">
            <path d="m6 17 10 5 10-5"/>
            <path d="m6 22 10 5 10-5"/>
            <path d="m6 12 10 5 10-5"/>
            <path d="M16 5v6m0 0-6 4m6-4 6 4"/>
            <circle cx="16" cy="5" r="2.4" fill="#3549c8"/>
            <circle cx="10" cy="15" r="2.4" fill="#3549c8"/>
            <circle cx="22" cy="15" r="2.4" fill="#3549c8"/>
          </g>
          <path d="m21 23 1.7 1.7 3.8-4.2" fill="none" stroke="#fff" stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2"/>
        </svg>
        """;
}
