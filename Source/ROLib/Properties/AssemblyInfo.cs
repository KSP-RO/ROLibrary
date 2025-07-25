﻿#define CIBUILD_disabled
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("ROLib")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ROLib")]
[assembly: AssemblyCopyright("Copyright © KSP-RO group 2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("fbb972b2-53ad-4174-a6e7-202e02645572")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("2.0.0.0")]    // Don't change for every release
#if CIBUILD
[assembly: AssemblyFileVersion("@MAJOR@.@MINOR@.@PATCH@.@BUILD@")]
[assembly: KSPAssembly("ROLib", @MAJOR@, @MINOR@, @PATCH@)]
#else
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: KSPAssembly("ROLib", 2, 0, 0)]
#endif

[assembly: KSPAssemblyDependency("TexturesUnlimited", 1, 6)]
[assembly: KSPAssemblyDependency("ROUtils", 1, 1)]
