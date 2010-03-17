using System.Reflection;

// Information about this assembly is defined by the following
// attributes.
//
// change them to the information which is associated with the assembly
// you compile.

[assembly : AssemblyTitle("LinFu.DynamicProxy")]
#if !SILVERLIGHT
[assembly : AssemblyDescription("A dynamic proxy library for the .NET Framework")]
#else
[assembly: AssemblyDescription("A dynamic proxy library for Silverlight and the .NET Framework")]
#endif

[assembly : AssemblyConfiguration("")]
[assembly : AssemblyCompany("")]
[assembly : AssemblyProduct("")]
[assembly : AssemblyCopyright("(c) 2007-2010 Philip Laureano")]
[assembly : AssemblyTrademark("")]
[assembly : AssemblyCulture("")]

// The assembly version has following format :
//
// Major.Minor.Build.Revision
//
// You can specify all values by your own or you can build default build and revision
// numbers with the '*' character (the default):

[assembly : AssemblyVersion("1.0.3.*")]

// The following attributes specify the key for the sign of your assembly. See the
// .NET Framework documentation for more information about signing.
// This is not required, if you don't want signing let these attributes like they're.

[assembly : AssemblyDelaySign(false)]