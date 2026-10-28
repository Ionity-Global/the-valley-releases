// THE VALLEY - publisher and version details stamped into the Windows programs (setup, launcher, World Server).
// Windows and virus scanners look at these: a program with a named publisher, product and version is far less likely to be
// treated as unknown or suspicious than a bare one.
// (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd | AEDI | Policy 986 AED | Author: Johan Wilhelm van Antwerp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyCompany("Ionity (Pty) Ltd")]
[assembly: AssemblyProduct("The Valley")]
[assembly: AssemblyCopyright("(c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd. All rights reserved.")]
[assembly: AssemblyTrademark("The Valley TM2 - www.ionity.today")]
[assembly: AssemblyVersion("4.15.0.0")]
[assembly: AssemblyFileVersion("4.15.0.0")]
[assembly: AssemblyInformationalVersion("4.15.0")]
[assembly: ComVisible(false)]
#if VALLEY_SETUP
[assembly: AssemblyTitle("The Valley Setup - installs and updates the game The Valley by Ionity")]
[assembly: AssemblyDescription("The Valley Setup - installs and updates the game The Valley by Ionity")]
#elif VALLEY_SERVER
[assembly: AssemblyTitle("The Valley World Server by Ionity")]
[assembly: AssemblyDescription("The Valley World Server by Ionity")]
#else
[assembly: AssemblyTitle("The Valley - game launcher by Ionity")]
[assembly: AssemblyDescription("The Valley - game launcher by Ionity")]
#endif
