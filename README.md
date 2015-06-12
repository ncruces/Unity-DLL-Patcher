# Unity3D DLL Patcher #

Patches DLLs compiled with Visual Studio for use with Unity 3D.

Specifically, it avoids ExecutionEngineExceptions in event handlers when used in platforms like iOS that require full AOT.


### Features ###

Replaces Interlocked.Exchange<T> and Interlocked.CompareExchange<T> calls with their non-generic versions. The generic versions raise an ExecutionEngineExceptions on Unity 3D iOS. This is what fixes event handlers as compiled by Visual Studio (they use CompareExchange internally).

Replaces System.IEnumConstraint, DelegateConstraint and ArrayConstraint for System.Enum, Delegate and Array in generic type constraints. See [Unconstrained Melody](https://github.com/jskeet/unconstrained-melody).

Also supports custom *.il patches.


### How does it work? ###

The project compiles down to an executable, which you can invoke as:

```PatchDlls assembly.dll [folder]```

PatchDlls will patch the specified assembly. The folder is optional. PatchDlls will scan that folder for *.il files, and will apply those patches to the disassembled IL before reassembly.


We use this as a post-build step in all our Visual Studio projects:

```$(SolutionDir)\Externals\PatchDlls\Binaries\PatchDlls.exe $(TargetPath) $(ProjectDir)```


### Custom *.il patches ###

Custom patches are IL files. The first and last line in the file are matched exactly within the disassembled IL. If a match is found, everything in between is replaced by the IL file contents.