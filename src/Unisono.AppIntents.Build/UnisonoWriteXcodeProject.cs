using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace Unisono.AppIntents.Build;

/// <summary>
/// Mirrors the app's Swift files (and the shared bridge) under OutputDir and writes a deterministic Xcode framework
/// project for them. Files are only touched when their content changed, so the SDK's input/output check stays honest.
/// Port of scripts/new-intents-xcodeproj.py; object ids are md5 of stable keys.
/// </summary>
public class UnisonoWriteXcodeProject : Microsoft.Build.Utilities.Task
{
    public ITaskItem[]? SwiftFiles { get; set; }
    public string SwiftRoot { get; set; } = "";
    public ITaskItem[]? SharedFiles { get; set; }
    /// <summary>Swift written by the generator; mirrored under Generated/ and compiled with the rest.</summary>
    public ITaskItem[]? GeneratedFiles { get; set; }
    public string OutputDir { get; set; } = "";
    public string Name { get; set; } = "";
    public string BundleId { get; set; } = "";
    public string DeploymentTarget { get; set; } = "17.0";
    public bool MacCatalyst { get; set; }
    [Output] public string ProjectPath { get; set; } = "";

    const string T = "\t";
    const string D = "$";

    public override bool Execute()
    {
        try
        {
            if (string.IsNullOrEmpty(DeploymentTarget)) DeploymentTarget = "17.0";
            var root = Path.GetFullPath(OutputDir);
            var swiftRoot = Path.GetFullPath(SwiftRoot).TrimEnd('/', '\\') + "/";
            Directory.CreateDirectory(root);

            // Sources/<relative path> for the app's files, Shared/<name> for the bridge.
            var planned = new List<(string Group, string Rel, string Source)>();
            foreach (var item in SwiftFiles ?? new ITaskItem[0])
            {
                var full = Path.GetFullPath(item.ItemSpec);
                var rel = full.StartsWith(swiftRoot, StringComparison.Ordinal) ? full.Substring(swiftRoot.Length) : Path.GetFileName(full);
                planned.Add(("Sources", rel.Replace('\\', '/'), full));
            }
            foreach (var item in SharedFiles ?? new ITaskItem[0])
            {
                var full = Path.GetFullPath(item.ItemSpec);
                planned.Add(("Shared", Path.GetFileName(full), full));
            }
            foreach (var item in GeneratedFiles ?? new ITaskItem[0])
            {
                var full = Path.GetFullPath(item.ItemSpec);
                planned.Add(("Generated", Path.GetFileName(full), full));
            }
            if (planned.Count == 0) { Log.LogError("Unisono: no Swift files to build."); return false; }

            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in planned)
            {
                var dest = Path.Combine(root, p.Group, p.Rel);
                expected.Add(dest);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                var bytes = File.ReadAllBytes(p.Source);
                if (!File.Exists(dest) || !File.ReadAllBytes(dest).SequenceEqual(bytes))
                    File.WriteAllBytes(dest, bytes);
            }
            foreach (var group in new[] { "Sources", "Shared", "Generated" })
            {
                var dir = Path.Combine(root, group);
                if (!Directory.Exists(dir)) continue;
                foreach (var stale in Directory.GetFiles(dir, "*.swift", SearchOption.AllDirectories))
                    if (!expected.Contains(stale)) File.Delete(stale);
            }

            var pbx = BuildPbxproj(planned);
            var projDir = Path.Combine(root, Name + ".xcodeproj");
            Directory.CreateDirectory(projDir);
            var pbxPath = Path.Combine(projDir, "project.pbxproj");
            if (!File.Exists(pbxPath) || File.ReadAllText(pbxPath) != pbx) File.WriteAllText(pbxPath, pbx);
            ProjectPath = projDir;
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true);
            return false;
        }
    }

    static string Oid(string key)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("X2"));
            return sb.ToString().Substring(0, 24);
        }
    }

    static string Q(string s) => s.Any(c => c == ' ' || c == '/' || c == '-' || c == '.') && !s.EndsWith(".swift") ? "\"" + s + "\"" : s;

    string BuildPbxproj(List<(string Group, string Rel, string Source)> planned)
    {
        var buildFiles = new List<string>();
        var fileRefs = new List<string>();
        var sourceEntries = new List<string>();
        var groupDefs = new List<string>();
        var mainChildren = new List<string>();

        // top-level group (Sources or Shared) -> subgroup -> file refs
        foreach (var top in new[] { "Sources", "Shared", "Generated" })
        {
            var files = planned.Where(p => p.Group == top).OrderBy(p => p.Rel, StringComparer.Ordinal).ToList();
            if (files.Count == 0) continue;
            var topId = Oid("group:" + top);
            var topChildren = new List<string>();
            var subgroups = new Dictionary<string, List<string>>();
            foreach (var f in files)
            {
                var parts = f.Rel.Split('/');
                var display = parts[parts.Length - 1];
                var key = top + "/" + f.Rel;
                var fr = Oid("fileref:" + key);
                var bf = Oid("buildfile:" + key);
                fileRefs.Add(T + T + fr + " /* " + display + " */ = {isa = PBXFileReference; lastKnownFileType = sourcecode.swift; path = " + Q(display) + "; sourceTree = \"<group>\"; };");
                buildFiles.Add(T + T + bf + " /* " + display + " in Sources */ = {isa = PBXBuildFile; fileRef = " + fr + " /* " + display + " */; };");
                sourceEntries.Add(T + T + T + T + bf + " /* " + display + " in Sources */,");
                if (parts.Length == 1)
                {
                    topChildren.Add(T + T + T + T + fr + " /* " + display + " */,");
                }
                else
                {
                    // one Xcode group per first folder level; deeper folders are flattened into it with their path
                    var sub = parts[0];
                    if (!subgroups.ContainsKey(sub)) subgroups[sub] = new List<string>();
                    subgroups[sub].Add(T + T + T + T + fr + " /* " + display + " */,");
                    if (parts.Length > 2)
                    {
                        // keep the real relative path below the subgroup so Xcode finds the file
                        var relBelow = string.Join("/", parts.Skip(1));
                        fileRefs[fileRefs.Count - 1] = T + T + fr + " /* " + display + " */ = {isa = PBXFileReference; lastKnownFileType = sourcecode.swift; name = " + Q(display) + "; path = \"" + relBelow + "\"; sourceTree = \"<group>\"; };";
                    }
                }
            }
            foreach (var sub in subgroups.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var gid = Oid("group:" + top + "/" + sub);
                topChildren.Add(T + T + T + T + gid + " /* " + sub + " */,");
                groupDefs.Add(T + T + gid + " /* " + sub + " */ = {\n" + T + T + T + "isa = PBXGroup;\n" + T + T + T + "children = (\n" + string.Join("\n", subgroups[sub]) + "\n" + T + T + T + ");\n" + T + T + T + "path = " + Q(sub) + ";\n" + T + T + T + "sourceTree = \"<group>\";\n" + T + T + "};");
            }
            groupDefs.Add(T + T + topId + " /* " + top + " */ = {\n" + T + T + T + "isa = PBXGroup;\n" + T + T + T + "children = (\n" + string.Join("\n", topChildren) + "\n" + T + T + T + ");\n" + T + T + T + "path = " + top + ";\n" + T + T + T + "sourceTree = \"<group>\";\n" + T + T + "};");
            mainChildren.Add(T + T + T + T + topId + " /* " + top + " */,");
        }

        var product = Oid("product");
        var frAppIntents = Oid("fileref:AppIntents.framework"); var bfAppIntents = Oid("buildfile:AppIntents.framework");
        var frFoundation = Oid("fileref:Foundation.framework"); var bfFoundation = Oid("buildfile:Foundation.framework");
        var mainGroup = Oid("group:main"); var frameworksGroup = Oid("group:Frameworks"); var productsGroup = Oid("group:Products");
        var phaseSources = Oid("phase:sources"); var phaseFrameworks = Oid("phase:frameworks"); var phaseHeaders = Oid("phase:headers");
        var target = Oid("target"); var project = Oid("project");
        var cfgListProject = Oid("cfglist:project"); var cfgListTarget = Oid("cfglist:target");
        var cfgPd = Oid("cfg:project:Debug"); var cfgPr = Oid("cfg:project:Release"); var cfgTd = Oid("cfg:target:Debug"); var cfgTr = Oid("cfg:target:Release");
        mainChildren.Add(T + T + T + T + frameworksGroup + " /* Frameworks */,");
        mainChildren.Add(T + T + T + T + productsGroup + " /* Products */,");

        var catalyst = MacCatalyst ? "\n" + T + T + T + T + "SUPPORTS_MACCATALYST = YES;\n" + T + T + T + T + "DERIVE_MACCATALYST_PRODUCT_BUNDLE_IDENTIFIER = YES;" : "";
        string TargetSettings() =>
            T+T+T+T+"CLANG_ENABLE_MODULES = YES;\n" +
            T+T+T+T+"CODE_SIGN_STYLE = Automatic;\n" +
            T+T+T+T+"CURRENT_PROJECT_VERSION = 1;\n" +
            T+T+T+T+"DEFINES_MODULE = YES;\n" +
            T+T+T+T+"DYLIB_COMPATIBILITY_VERSION = 1;\n" +
            T+T+T+T+"DYLIB_CURRENT_VERSION = 1;\n" +
            T+T+T+T+"DYLIB_INSTALL_NAME_BASE = \"@rpath\";\n" +
            T+T+T+T+"GENERATE_INFOPLIST_FILE = YES;\n" +
            T+T+T+T+"INFOPLIST_KEY_NSHumanReadableCopyright = \"\";\n" +
            T+T+T+T+"INSTALL_PATH = \"" + D + "(LOCAL_LIBRARY_DIR)/Frameworks\";\n" +
            T+T+T+T+"IPHONEOS_DEPLOYMENT_TARGET = " + DeploymentTarget + ";\n" +
            T+T+T+T+"LD_RUNPATH_SEARCH_PATHS = (\n" + T+T+T+T+T+"\"" + D + "(inherited)\",\n" + T+T+T+T+T+"\"@executable_path/Frameworks\",\n" + T+T+T+T+T+"\"@loader_path/Frameworks\",\n" + T+T+T+T+");\n" +
            T+T+T+T+"MARKETING_VERSION = 1.0;\n" +
            T+T+T+T+"PRODUCT_BUNDLE_IDENTIFIER = " + BundleId + ";\n" +
            T+T+T+T+"PRODUCT_NAME = \"" + D + "(TARGET_NAME:c99extidentifier)\";\n" +
            T+T+T+T+"SKIP_INSTALL = NO;" + catalyst + "\n" +
            T+T+T+T+"SWIFT_EMIT_LOC_STRINGS = YES;\n" +
            T+T+T+T+"SWIFT_INSTALL_OBJC_HEADER = YES;\n" +
            T+T+T+T+"SWIFT_REFLECTION_METADATA_LEVEL = all;\n" +
            T+T+T+T+"SWIFT_VERSION = 5.0;\n" +
            T+T+T+T+"TARGETED_DEVICE_FAMILY = \"1,2\";";
        string ProjectSettings(bool release)
        {
            var common = T+T+T+T+"ALWAYS_SEARCH_USER_PATHS = NO;\n" + T+T+T+T+"CLANG_CXX_LANGUAGE_STANDARD = \"gnu++20\";\n" + T+T+T+T+"CLANG_ENABLE_OBJC_ARC = YES;\n" + T+T+T+T+"COPY_PHASE_STRIP = NO;\n" + T+T+T+T+"IPHONEOS_DEPLOYMENT_TARGET = " + DeploymentTarget + ";\n" + T+T+T+T+"SDKROOT = iphoneos;";
            if (release)
                return common + "\n" + T+T+T+T+"DEBUG_INFORMATION_FORMAT = \"dwarf-with-dsym\";\n" + T+T+T+T+"GCC_OPTIMIZATION_LEVEL = s;\n" + T+T+T+T+"SWIFT_COMPILATION_MODE = wholemodule;\n" + T+T+T+T+"SWIFT_OPTIMIZATION_LEVEL = \"-O\";\n" + T+T+T+T+"VALIDATE_PRODUCT = YES;";
            return common + "\n" + T+T+T+T+"DEBUG_INFORMATION_FORMAT = dwarf;\n" + T+T+T+T+"ENABLE_TESTABILITY = YES;\n" + T+T+T+T+"GCC_OPTIMIZATION_LEVEL = 0;\n" + T+T+T+T+"GCC_PREPROCESSOR_DEFINITIONS = (\n" + T+T+T+T+T+"\"DEBUG=1\",\n" + T+T+T+T+T+"\"" + D + "(inherited)\",\n" + T+T+T+T+");\n" + T+T+T+T+"ONLY_ACTIVE_ARCH = YES;\n" + T+T+T+T+"SWIFT_ACTIVE_COMPILATION_CONDITIONS = \"" + D + "(inherited) DEBUG\";\n" + T+T+T+T+"SWIFT_OPTIMIZATION_LEVEL = \"-Onone\";";
        }

        var sb = new StringBuilder();
        sb.Append("// !$*UTF8*$!\n{\n").Append(T).Append("archiveVersion = 1;\n").Append(T).Append("classes = {\n").Append(T).Append("};\n").Append(T).Append("objectVersion = 56;\n").Append(T).Append("objects = {\n\n");
        sb.Append("/* Begin PBXBuildFile section */\n").Append(string.Join("\n", buildFiles)).Append("\n");
        sb.Append(T+T+bfAppIntents+" /* AppIntents.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = "+frAppIntents+" /* AppIntents.framework */; };\n");
        sb.Append(T+T+bfFoundation+" /* Foundation.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = "+frFoundation+" /* Foundation.framework */; };\n");
        sb.Append("/* End PBXBuildFile section */\n\n");
        sb.Append("/* Begin PBXFileReference section */\n");
        sb.Append(T+T+product+" /* "+Name+".framework */ = {isa = PBXFileReference; explicitFileType = wrapper.framework; includeInIndex = 0; path = "+Name+".framework; sourceTree = BUILT_PRODUCTS_DIR; };\n");
        sb.Append(string.Join("\n", fileRefs)).Append("\n");
        sb.Append(T+T+frAppIntents+" /* AppIntents.framework */ = {isa = PBXFileReference; lastKnownFileType = wrapper.framework; name = AppIntents.framework; path = System/Library/Frameworks/AppIntents.framework; sourceTree = SDKROOT; };\n");
        sb.Append(T+T+frFoundation+" /* Foundation.framework */ = {isa = PBXFileReference; lastKnownFileType = wrapper.framework; name = Foundation.framework; path = System/Library/Frameworks/Foundation.framework; sourceTree = SDKROOT; };\n");
        sb.Append("/* End PBXFileReference section */\n\n");
        sb.Append("/* Begin PBXFrameworksBuildPhase section */\n");
        sb.Append(T+T+phaseFrameworks+" /* Frameworks */ = {\n"+T+T+T+"isa = PBXFrameworksBuildPhase;\n"+T+T+T+"buildActionMask = 2147483647;\n"+T+T+T+"files = (\n"+T+T+T+T+bfAppIntents+" /* AppIntents.framework in Frameworks */,\n"+T+T+T+T+bfFoundation+" /* Foundation.framework in Frameworks */,\n"+T+T+T+");\n"+T+T+T+"runOnlyForDeploymentPostprocessing = 0;\n"+T+T+"};\n");
        sb.Append("/* End PBXFrameworksBuildPhase section */\n\n");
        sb.Append("/* Begin PBXGroup section */\n");
        sb.Append(T+T+mainGroup+" = {\n"+T+T+T+"isa = PBXGroup;\n"+T+T+T+"children = (\n"+string.Join("\n", mainChildren)+"\n"+T+T+T+");\n"+T+T+T+"sourceTree = \"<group>\";\n"+T+T+"};\n");
        sb.Append(string.Join("\n", groupDefs)).Append("\n");
        sb.Append(T+T+frameworksGroup+" /* Frameworks */ = {\n"+T+T+T+"isa = PBXGroup;\n"+T+T+T+"children = (\n"+T+T+T+T+frAppIntents+" /* AppIntents.framework */,\n"+T+T+T+T+frFoundation+" /* Foundation.framework */,\n"+T+T+T+");\n"+T+T+T+"name = Frameworks;\n"+T+T+T+"sourceTree = \"<group>\";\n"+T+T+"};\n");
        sb.Append(T+T+productsGroup+" /* Products */ = {\n"+T+T+T+"isa = PBXGroup;\n"+T+T+T+"children = (\n"+T+T+T+T+product+" /* "+Name+".framework */,\n"+T+T+T+");\n"+T+T+T+"name = Products;\n"+T+T+T+"sourceTree = \"<group>\";\n"+T+T+"};\n");
        sb.Append("/* End PBXGroup section */\n\n");
        sb.Append("/* Begin PBXHeadersBuildPhase section */\n");
        sb.Append(T+T+phaseHeaders+" /* Headers */ = {\n"+T+T+T+"isa = PBXHeadersBuildPhase;\n"+T+T+T+"buildActionMask = 2147483647;\n"+T+T+T+"files = (\n"+T+T+T+");\n"+T+T+T+"runOnlyForDeploymentPostprocessing = 0;\n"+T+T+"};\n");
        sb.Append("/* End PBXHeadersBuildPhase section */\n\n");
        sb.Append("/* Begin PBXNativeTarget section */\n");
        sb.Append(T+T+target+" /* "+Name+" */ = {\n"+T+T+T+"isa = PBXNativeTarget;\n"+T+T+T+"buildConfigurationList = "+cfgListTarget+" /* Build configuration list for PBXNativeTarget \""+Name+"\" */;\n"+T+T+T+"buildPhases = (\n"+T+T+T+T+phaseSources+" /* Sources */,\n"+T+T+T+T+phaseFrameworks+" /* Frameworks */,\n"+T+T+T+T+phaseHeaders+" /* Headers */,\n"+T+T+T+");\n"+T+T+T+"buildRules = (\n"+T+T+T+");\n"+T+T+T+"dependencies = (\n"+T+T+T+");\n"+T+T+T+"name = "+Name+";\n"+T+T+T+"productName = "+Name+";\n"+T+T+T+"productReference = "+product+" /* "+Name+".framework */;\n"+T+T+T+"productType = \"com.apple.product-type.framework\";\n"+T+T+"};\n");
        sb.Append("/* End PBXNativeTarget section */\n\n");
        sb.Append("/* Begin PBXProject section */\n");
        sb.Append(T+T+project+" /* Project object */ = {\n"+T+T+T+"isa = PBXProject;\n"+T+T+T+"buildConfigurationList = "+cfgListProject+" /* Build configuration list for PBXProject \""+Name+"\" */;\n"+T+T+T+"compatibilityVersion = \"Xcode 14.0\";\n"+T+T+T+"developmentRegion = en;\n"+T+T+T+"hasScannedForEncodings = 0;\n"+T+T+T+"knownRegions = (\n"+T+T+T+T+"en,\n"+T+T+T+T+"Base,\n"+T+T+T+");\n"+T+T+T+"mainGroup = "+mainGroup+";\n"+T+T+T+"productRefGroup = "+productsGroup+" /* Products */;\n"+T+T+T+"projectDirPath = \"\";\n"+T+T+T+"projectRoot = \"\";\n"+T+T+T+"targets = (\n"+T+T+T+T+target+" /* "+Name+" */,\n"+T+T+T+");\n"+T+T+"};\n");
        sb.Append("/* End PBXProject section */\n\n");
        sb.Append("/* Begin PBXSourcesBuildPhase section */\n");
        sb.Append(T+T+phaseSources+" /* Sources */ = {\n"+T+T+T+"isa = PBXSourcesBuildPhase;\n"+T+T+T+"buildActionMask = 2147483647;\n"+T+T+T+"files = (\n"+string.Join("\n", sourceEntries)+"\n"+T+T+T+");\n"+T+T+T+"runOnlyForDeploymentPostprocessing = 0;\n"+T+T+"};\n");
        sb.Append("/* End PBXSourcesBuildPhase section */\n\n");
        sb.Append("/* Begin XCBuildConfiguration section */\n");
        sb.Append(T+T+cfgPd+" /* Debug */ = {\n"+T+T+T+"isa = XCBuildConfiguration;\n"+T+T+T+"buildSettings = {\n"+ProjectSettings(false)+"\n"+T+T+T+"};\n"+T+T+T+"name = Debug;\n"+T+T+"};\n");
        sb.Append(T+T+cfgPr+" /* Release */ = {\n"+T+T+T+"isa = XCBuildConfiguration;\n"+T+T+T+"buildSettings = {\n"+ProjectSettings(true)+"\n"+T+T+T+"};\n"+T+T+T+"name = Release;\n"+T+T+"};\n");
        sb.Append(T+T+cfgTd+" /* Debug */ = {\n"+T+T+T+"isa = XCBuildConfiguration;\n"+T+T+T+"buildSettings = {\n"+TargetSettings()+"\n"+T+T+T+"};\n"+T+T+T+"name = Debug;\n"+T+T+"};\n");
        sb.Append(T+T+cfgTr+" /* Release */ = {\n"+T+T+T+"isa = XCBuildConfiguration;\n"+T+T+T+"buildSettings = {\n"+TargetSettings()+"\n"+T+T+T+"};\n"+T+T+T+"name = Release;\n"+T+T+"};\n");
        sb.Append("/* End XCBuildConfiguration section */\n\n");
        sb.Append("/* Begin XCConfigurationList section */\n");
        sb.Append(T+T+cfgListProject+" /* Build configuration list for PBXProject \""+Name+"\" */ = {\n"+T+T+T+"isa = XCConfigurationList;\n"+T+T+T+"buildConfigurations = (\n"+T+T+T+T+cfgPd+" /* Debug */,\n"+T+T+T+T+cfgPr+" /* Release */,\n"+T+T+T+");\n"+T+T+T+"defaultConfigurationIsVisible = 0;\n"+T+T+T+"defaultConfigurationName = Release;\n"+T+T+"};\n");
        sb.Append(T+T+cfgListTarget+" /* Build configuration list for PBXNativeTarget \""+Name+"\" */ = {\n"+T+T+T+"isa = XCConfigurationList;\n"+T+T+T+"buildConfigurations = (\n"+T+T+T+T+cfgTd+" /* Debug */,\n"+T+T+T+T+cfgTr+" /* Release */,\n"+T+T+T+");\n"+T+T+T+"defaultConfigurationIsVisible = 0;\n"+T+T+T+"defaultConfigurationName = Release;\n"+T+T+"};\n");
        sb.Append("/* End XCConfigurationList section */\n");
        sb.Append(T).Append("};\n").Append(T).Append("rootObject = ").Append(project).Append(" /* Project object */;\n}\n");
        return sb.ToString();
    }
}
