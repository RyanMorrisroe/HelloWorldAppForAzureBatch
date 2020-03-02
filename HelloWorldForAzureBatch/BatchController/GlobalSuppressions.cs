// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "It's a demo project, doesn't matter")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "It's a demo project, doens't matter")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Storage accounts require lowercase")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5377:Use Container Level Access Policy", Justification = "Normally you would as long as you can use Azure AD everywhere")]