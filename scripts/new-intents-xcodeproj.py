#!/usr/bin/env python3
"""Generate the Xcode project for an app's Unisono intents framework.

Usage:
  scripts/new-intents-xcodeproj.py <project-dir> --name <TargetName> --bundle-id <id>
                                   [--deployment-target 17.0] [--maccatalyst]
                                   [--shared <swift-file-relative-to-project-dir>]...

Scans <project-dir>/Sources/**/*.swift (one Xcode group per top-level folder) and writes
<project-dir>/<TargetName>.xcodeproj/project.pbxproj. --shared adds Swift files that live outside the
folder (for example ../../../src/Unisono.AppIntents/native/UnisonoBridge.swift) so the bridge is referenced,
not copied. Object ids are derived from names, so re-running produces the same file. Plain Python 3.
This is the shape the M2 code generator will drive; until then it is run by hand.
"""
import argparse
import hashlib
import os
import sys


def oid(key: str) -> str:
    return hashlib.md5(key.encode("utf-8")).hexdigest()[:24].upper()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("project_dir")
    ap.add_argument("--name", required=True, help="target / module / framework name, e.g. UnisonoNotesIntents")
    ap.add_argument("--bundle-id", required=True)
    ap.add_argument("--deployment-target", default="17.0")
    ap.add_argument("--maccatalyst", action="store_true", help="also allow building for Mac Catalyst")
    ap.add_argument("--shared", action="append", default=[], help="extra .swift file, path relative to project dir")
    args = ap.parse_args()

    root = os.path.abspath(args.project_dir)
    sources = os.path.join(root, "Sources")
    if not os.path.isdir(sources):
        print(f"no Sources folder in {root}", file=sys.stderr)
        return 2

    # Collect Sources/<Group>/<File>.swift and Sources/<File>.swift
    groups: dict[str, list[str]] = {}
    for dirpath, _dirs, files in os.walk(sources):
        rel = os.path.relpath(dirpath, sources)
        group = "" if rel == "." else rel.split(os.sep)[0]
        for f in sorted(files):
            if f.endswith(".swift"):
                groups.setdefault(group, []).append(os.path.relpath(os.path.join(dirpath, f), sources))
    if not groups:
        print("no .swift files found", file=sys.stderr)
        return 2

    name = args.name
    T = "\t"
    build_files, file_refs, group_defs, source_entries = [], [], [], []

    def add_source(ref_key: str, display: str, path_in_group: str, external: bool = False):
        fr, bf = oid("fileref:" + ref_key), oid("buildfile:" + ref_key)
        path_attr = f'path = "{path_in_group}";' if any(c in path_in_group for c in " /") and external else f"path = {path_in_group};"
        name_attr = f" name = {display};" if external else ""
        file_refs.append(f"{T*2}{fr} /* {display} */ = {{isa = PBXFileReference; lastKnownFileType = sourcecode.swift;{name_attr} {path_attr} sourceTree = \"<group>\"; }};")
        build_files.append(f"{T*2}{bf} /* {display} in Sources */ = {{isa = PBXBuildFile; fileRef = {fr} /* {display} */; }};")
        source_entries.append(f"{T*4}{bf} /* {display} in Sources */,")
        return fr

    sources_children = []
    for group in sorted(groups):
        refs = []
        for rel in groups[group]:
            display = os.path.basename(rel)
            refs.append(add_source("Sources/" + rel, display, display))
        if group == "":
            sources_children.extend(refs)
        else:
            gid = oid("group:" + group)
            sources_children.append(f"{T*4}{gid} /* {group} */,")
            children = "\n".join(f"{T*4}{r} /* {os.path.basename(groups[group][i])} */," for i, r in enumerate(refs))
            group_defs.append(f"{T*2}{gid} /* {group} */ = {{\n{T*3}isa = PBXGroup;\n{T*3}children = (\n{children}\n{T*3});\n{T*3}path = {group};\n{T*3}sourceTree = \"<group>\";\n{T*2}}};")
    # plain files directly under Sources need the "<id> /* name */," form too
    sources_children = [c if c.startswith(T * 4) else f"{T*4}{c} /* {os.path.basename(groups[''][i])} */," for i, c in enumerate(sources_children)]

    shared_children = []
    for rel in args.shared:
        display = os.path.basename(rel)
        fr = add_source("shared:" + rel, display, rel.replace("\\", "/"), external=True)
        shared_children.append(f"{T*4}{fr} /* {display} */,")

    product = oid("product")
    fr_appintents, bf_appintents = oid("fileref:AppIntents.framework"), oid("buildfile:AppIntents.framework")
    fr_foundation, bf_foundation = oid("fileref:Foundation.framework"), oid("buildfile:Foundation.framework")
    main_group, sources_group, shared_group, frameworks_group, products_group = (oid("group:main"), oid("group:Sources"), oid("group:Shared"), oid("group:Frameworks"), oid("group:Products"))
    phase_sources, phase_frameworks, phase_headers = oid("phase:sources"), oid("phase:frameworks"), oid("phase:headers")
    target, project = oid("target"), oid("project")
    cfg_list_project, cfg_list_target = oid("cfglist:project"), oid("cfglist:target")
    cfg_pd, cfg_pr, cfg_td, cfg_tr = oid("cfg:project:Debug"), oid("cfg:project:Release"), oid("cfg:target:Debug"), oid("cfg:target:Release")

    catalyst = f"\n{T*4}SUPPORTS_MACCATALYST = YES;\n{T*4}DERIVE_MACCATALYST_PRODUCT_BUNDLE_IDENTIFIER = YES;" if args.maccatalyst else ""

    def target_settings(release: bool) -> str:
        return f"""{T*4}CLANG_ENABLE_MODULES = YES;
{T*4}CODE_SIGN_STYLE = Automatic;
{T*4}CURRENT_PROJECT_VERSION = 1;
{T*4}DEFINES_MODULE = YES;
{T*4}DYLIB_COMPATIBILITY_VERSION = 1;
{T*4}DYLIB_CURRENT_VERSION = 1;
{T*4}DYLIB_INSTALL_NAME_BASE = "@rpath";
{T*4}GENERATE_INFOPLIST_FILE = YES;
{T*4}INFOPLIST_KEY_NSHumanReadableCopyright = "";
{T*4}INSTALL_PATH = "$(LOCAL_LIBRARY_DIR)/Frameworks";
{T*4}IPHONEOS_DEPLOYMENT_TARGET = {args.deployment_target};
{T*4}LD_RUNPATH_SEARCH_PATHS = (
{T*5}"$(inherited)",
{T*5}"@executable_path/Frameworks",
{T*5}"@loader_path/Frameworks",
{T*4});
{T*4}MARKETING_VERSION = 1.0;
{T*4}PRODUCT_BUNDLE_IDENTIFIER = {args.bundle_id};
{T*4}PRODUCT_NAME = "$(TARGET_NAME:c99extidentifier)";
{T*4}SKIP_INSTALL = NO;{catalyst}
{T*4}SWIFT_EMIT_LOC_STRINGS = YES;
{T*4}SWIFT_INSTALL_OBJC_HEADER = YES;
{T*4}SWIFT_REFLECTION_METADATA_LEVEL = all;
{T*4}SWIFT_VERSION = 5.0;
{T*4}TARGETED_DEVICE_FAMILY = "1,2";"""

    def project_settings(release: bool) -> str:
        common = f"""{T*4}ALWAYS_SEARCH_USER_PATHS = NO;
{T*4}CLANG_CXX_LANGUAGE_STANDARD = "gnu++20";
{T*4}CLANG_ENABLE_OBJC_ARC = YES;
{T*4}COPY_PHASE_STRIP = NO;
{T*4}IPHONEOS_DEPLOYMENT_TARGET = {args.deployment_target};
{T*4}SDKROOT = iphoneos;"""
        if release:
            return common + f"""
{T*4}DEBUG_INFORMATION_FORMAT = "dwarf-with-dsym";
{T*4}GCC_OPTIMIZATION_LEVEL = s;
{T*4}SWIFT_COMPILATION_MODE = wholemodule;
{T*4}SWIFT_OPTIMIZATION_LEVEL = "-O";
{T*4}VALIDATE_PRODUCT = YES;"""
        return common + f"""
{T*4}DEBUG_INFORMATION_FORMAT = dwarf;
{T*4}ENABLE_TESTABILITY = YES;
{T*4}GCC_OPTIMIZATION_LEVEL = 0;
{T*4}GCC_PREPROCESSOR_DEFINITIONS = (
{T*5}"DEBUG=1",
{T*5}"$(inherited)",
{T*4});
{T*4}ONLY_ACTIVE_ARCH = YES;
{T*4}SWIFT_ACTIVE_COMPILATION_CONDITIONS = "$(inherited) DEBUG";
{T*4}SWIFT_OPTIMIZATION_LEVEL = "-Onone";"""

    nl = "\n"
    main_children = [f"{T*4}{sources_group} /* Sources */,"]
    if shared_children:
        main_children.append(f"{T*4}{shared_group} /* Shared */,")
    main_children += [f"{T*4}{frameworks_group} /* Frameworks */,", f"{T*4}{products_group} /* Products */,"]
    shared_group_def = "" if not shared_children else f"""{T*2}{shared_group} /* Shared */ = {{
{T*3}isa = PBXGroup;
{T*3}children = (
{nl.join(shared_children)}
{T*3});
{T*3}name = Shared;
{T*3}sourceTree = "<group>";
{T*2}}};
"""

    pbx = f"""// !$*UTF8*$!
{{
{T}archiveVersion = 1;
{T}classes = {{
{T}}};
{T}objectVersion = 56;
{T}objects = {{

/* Begin PBXBuildFile section */
{nl.join(build_files)}
{T*2}{bf_appintents} /* AppIntents.framework in Frameworks */ = {{isa = PBXBuildFile; fileRef = {fr_appintents} /* AppIntents.framework */; }};
{T*2}{bf_foundation} /* Foundation.framework in Frameworks */ = {{isa = PBXBuildFile; fileRef = {fr_foundation} /* Foundation.framework */; }};
/* End PBXBuildFile section */

/* Begin PBXFileReference section */
{T*2}{product} /* {name}.framework */ = {{isa = PBXFileReference; explicitFileType = wrapper.framework; includeInIndex = 0; path = {name}.framework; sourceTree = BUILT_PRODUCTS_DIR; }};
{nl.join(file_refs)}
{T*2}{fr_appintents} /* AppIntents.framework */ = {{isa = PBXFileReference; lastKnownFileType = wrapper.framework; name = AppIntents.framework; path = System/Library/Frameworks/AppIntents.framework; sourceTree = SDKROOT; }};
{T*2}{fr_foundation} /* Foundation.framework */ = {{isa = PBXFileReference; lastKnownFileType = wrapper.framework; name = Foundation.framework; path = System/Library/Frameworks/Foundation.framework; sourceTree = SDKROOT; }};
/* End PBXFileReference section */

/* Begin PBXFrameworksBuildPhase section */
{T*2}{phase_frameworks} /* Frameworks */ = {{
{T*3}isa = PBXFrameworksBuildPhase;
{T*3}buildActionMask = 2147483647;
{T*3}files = (
{T*4}{bf_appintents} /* AppIntents.framework in Frameworks */,
{T*4}{bf_foundation} /* Foundation.framework in Frameworks */,
{T*3});
{T*3}runOnlyForDeploymentPostprocessing = 0;
{T*2}}};
/* End PBXFrameworksBuildPhase section */

/* Begin PBXGroup section */
{T*2}{main_group} = {{
{T*3}isa = PBXGroup;
{T*3}children = (
{nl.join(main_children)}
{T*3});
{T*3}sourceTree = "<group>";
{T*2}}};
{T*2}{sources_group} /* Sources */ = {{
{T*3}isa = PBXGroup;
{T*3}children = (
{nl.join(sources_children)}
{T*3});
{T*3}path = Sources;
{T*3}sourceTree = "<group>";
{T*2}}};
{nl.join(group_defs)}
{shared_group_def}{T*2}{frameworks_group} /* Frameworks */ = {{
{T*3}isa = PBXGroup;
{T*3}children = (
{T*4}{fr_appintents} /* AppIntents.framework */,
{T*4}{fr_foundation} /* Foundation.framework */,
{T*3});
{T*3}name = Frameworks;
{T*3}sourceTree = "<group>";
{T*2}}};
{T*2}{products_group} /* Products */ = {{
{T*3}isa = PBXGroup;
{T*3}children = (
{T*4}{product} /* {name}.framework */,
{T*3});
{T*3}name = Products;
{T*3}sourceTree = "<group>";
{T*2}}};
/* End PBXGroup section */

/* Begin PBXHeadersBuildPhase section */
{T*2}{phase_headers} /* Headers */ = {{
{T*3}isa = PBXHeadersBuildPhase;
{T*3}buildActionMask = 2147483647;
{T*3}files = (
{T*3});
{T*3}runOnlyForDeploymentPostprocessing = 0;
{T*2}}};
/* End PBXHeadersBuildPhase section */

/* Begin PBXNativeTarget section */
{T*2}{target} /* {name} */ = {{
{T*3}isa = PBXNativeTarget;
{T*3}buildConfigurationList = {cfg_list_target} /* Build configuration list for PBXNativeTarget "{name}" */;
{T*3}buildPhases = (
{T*4}{phase_sources} /* Sources */,
{T*4}{phase_frameworks} /* Frameworks */,
{T*4}{phase_headers} /* Headers */,
{T*3});
{T*3}buildRules = (
{T*3});
{T*3}dependencies = (
{T*3});
{T*3}name = {name};
{T*3}productName = {name};
{T*3}productReference = {product} /* {name}.framework */;
{T*3}productType = "com.apple.product-type.framework";
{T*2}}};
/* End PBXNativeTarget section */

/* Begin PBXProject section */
{T*2}{project} /* Project object */ = {{
{T*3}isa = PBXProject;
{T*3}buildConfigurationList = {cfg_list_project} /* Build configuration list for PBXProject "{name}" */;
{T*3}compatibilityVersion = "Xcode 14.0";
{T*3}developmentRegion = en;
{T*3}hasScannedForEncodings = 0;
{T*3}knownRegions = (
{T*4}en,
{T*4}Base,
{T*3});
{T*3}mainGroup = {main_group};
{T*3}productRefGroup = {products_group} /* Products */;
{T*3}projectDirPath = "";
{T*3}projectRoot = "";
{T*3}targets = (
{T*4}{target} /* {name} */,
{T*3});
{T*2}}};
/* End PBXProject section */

/* Begin PBXSourcesBuildPhase section */
{T*2}{phase_sources} /* Sources */ = {{
{T*3}isa = PBXSourcesBuildPhase;
{T*3}buildActionMask = 2147483647;
{T*3}files = (
{nl.join(source_entries)}
{T*3});
{T*3}runOnlyForDeploymentPostprocessing = 0;
{T*2}}};
/* End PBXSourcesBuildPhase section */

/* Begin XCBuildConfiguration section */
{T*2}{cfg_pd} /* Debug */ = {{
{T*3}isa = XCBuildConfiguration;
{T*3}buildSettings = {{
{project_settings(False)}
{T*3}}};
{T*3}name = Debug;
{T*2}}};
{T*2}{cfg_pr} /* Release */ = {{
{T*3}isa = XCBuildConfiguration;
{T*3}buildSettings = {{
{project_settings(True)}
{T*3}}};
{T*3}name = Release;
{T*2}}};
{T*2}{cfg_td} /* Debug */ = {{
{T*3}isa = XCBuildConfiguration;
{T*3}buildSettings = {{
{target_settings(False)}
{T*3}}};
{T*3}name = Debug;
{T*2}}};
{T*2}{cfg_tr} /* Release */ = {{
{T*3}isa = XCBuildConfiguration;
{T*3}buildSettings = {{
{target_settings(True)}
{T*3}}};
{T*3}name = Release;
{T*2}}};
/* End XCBuildConfiguration section */

/* Begin XCConfigurationList section */
{T*2}{cfg_list_project} /* Build configuration list for PBXProject "{name}" */ = {{
{T*3}isa = XCConfigurationList;
{T*3}buildConfigurations = (
{T*4}{cfg_pd} /* Debug */,
{T*4}{cfg_pr} /* Release */,
{T*3});
{T*3}defaultConfigurationIsVisible = 0;
{T*3}defaultConfigurationName = Release;
{T*2}}};
{T*2}{cfg_list_target} /* Build configuration list for PBXNativeTarget "{name}" */ = {{
{T*3}isa = XCConfigurationList;
{T*3}buildConfigurations = (
{T*4}{cfg_td} /* Debug */,
{T*4}{cfg_tr} /* Release */,
{T*3});
{T*3}defaultConfigurationIsVisible = 0;
{T*3}defaultConfigurationName = Release;
{T*2}}};
/* End XCConfigurationList section */
{T}}};
{T}rootObject = {project} /* Project object */;
}}
"""
    out_dir = os.path.join(root, f"{name}.xcodeproj")
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, "project.pbxproj")
    with open(out, "w", encoding="utf-8") as fh:
        fh.write(pbx)
    total = sum(len(v) for v in groups.values()) + len(args.shared)
    print(f"wrote {os.path.relpath(out)} with {total} Swift files in {len(groups)} group(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
