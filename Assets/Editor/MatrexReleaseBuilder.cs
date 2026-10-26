#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class MatrexReleaseBuilder
{
 public static void Build()
 {
  string[] args=Environment.GetCommandLineArgs();
  string Arg(string key,string fallback=null) { int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback; }
  string output=Arg("-releaseOutput"); string version=Arg("-releaseVersion","0.1.0");
  BuildTarget target=(BuildTarget)Enum.Parse(typeof(BuildTarget),Arg("-releaseTarget","StandaloneOSX"));
  if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target)) throw new Exception("Missing build module: "+target);
  PlayerSettings.productName="MATREX VR";PlayerSettings.bundleVersion=version;
  PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;PlayerSettings.defaultIsNativeResolution=true;
  PlayerSettings.macRetinaSupport=true;PlayerSettings.resizableWindow=true;PlayerSettings.allowFullscreenSwitch=false;
  if(Array.IndexOf(args,"-localDesktopBuild")>=0) PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"org.pvnkmrksk.matrexvr.local");
  PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
  if(target==BuildTarget.StandaloneOSX)EditorUserBuildSettings.SetPlatformSettings("Standalone","OSXUniversal","Architecture","x64ARM64");
  string[] scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).Distinct().ToArray();
  // Keep old-tag builder injection working: this method does not depend on a new runtime type.
  string identityPath="Assets/StreamingAssets/player_build_info.json";
  var identity=File.Exists(identityPath)?Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(identityPath)):new Newtonsoft.Json.Linq.JObject { ["schemaVersion"]=1,["sourceRevision"]=null,["sourceState"]="unknown" };
  identity["applicationVersion"]=version;identity["unityVersion"]=Application.unityVersion;identity["buildTarget"]=target.ToString();
  Directory.CreateDirectory(Path.GetDirectoryName(identityPath));File.WriteAllText(identityPath,identity.ToString(Newtonsoft.Json.Formatting.Indented)+"\n");
  AssetDatabase.ImportAsset(identityPath,ImportAssetOptions.ForceSynchronousImport);
  Directory.CreateDirectory(Path.GetDirectoryName(output));
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=output,target=target,options=BuildOptions.None});
  var summary=new {version,target=target.ToString(),result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,buildGuid=report.summary.guid.ToString(),scenes=scenes};
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-report.json"),Newtonsoft.Json.JsonConvert.SerializeObject(summary,Newtonsoft.Json.Formatting.Indented));
  if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
 }
}
#endif
