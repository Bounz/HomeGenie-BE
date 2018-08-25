using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HomeGenie.Automation.Scripting;
using HomeGenie.Data;
using HomeGenie.Service;
using HomeGenie.Service.Constants;

namespace HomeGenie.Automation.Engines
{
    public class CSharpEngine : ProgramEngineBase, IProgramEngine
    {
        // c# program fields
        private AppDomain _programDomain;
        private Type _assemblyType;
        private Object _scriptInstance;
        private MethodInfo _methodRun;
        private MethodInfo _methodReset;
        private MethodInfo _methodEvaluateCondition;
        private Assembly _scriptAssembly;

        private static bool IsShadowCopySet;

        public CSharpEngine(ProgramBlock programBlock) : base(programBlock)
        {
            // TODO: SetShadowCopyPath/SetShadowCopyFiles methods are deprecated... 
            // TODO: create own AppDomain for "programDomain" instead of using CurrentDomain
            // TODO: and use AppDomainSetup to set shadow copy for each app domain
            // TODO: !!! verify AppDomain compatibility with mono !!!
            if (!IsShadowCopySet)
            {
                IsShadowCopySet = true;
                var domain = AppDomain.CurrentDomain;
                domain.SetShadowCopyPath(FilePaths.ProgramsFolder);
                domain.SetShadowCopyFiles();
            }
        }

        public bool Load()
        {
            var success = LoadAssembly();
            if (!success)
            {
                ProgramBlock.ScriptErrors = "Program update is required.";
            }
            return success;
        }

        public void Unload()
        {
            Reset();
            ProgramBlock.ActivationTime = null;
            ProgramBlock.TriggerTime = null;
            if (_programDomain != null)
            {
                // Unloading program app domain...
                try { AppDomain.Unload(_programDomain); }
                catch
                {
                    // ignored
                }
                _programDomain = null;
            }
        }

        public List<ProgramError> Compile()
        {
            var errors = new List<ProgramError>();

            // check for output directory
            if (!Directory.Exists(Path.GetDirectoryName(AssemblyFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssemblyFile));
            }

            // dispose assembly and interrupt current task (if any)
            ProgramBlock.IsEnabled = false;

            // clean up old assembly files
            try
            {
                // If the file to be deleted does not exist, no exception is thrown.
                File.Delete(AssemblyFile);
                File.Delete(AssemblyFile + ".mdb");
                File.Delete(AssemblyFile.Replace(".dll", ".mdb"));
                File.Delete(AssemblyFile + ".pdb");
                File.Delete(AssemblyFile.Replace(".dll", ".pdb"));
            }
            catch (Exception ex)
            {
                HomeGenieService.LogError(ex);
            }

            // DO NOT CHANGE THE FOLLOWING LINES OF CODE
            // it is a lil' trick for mono compatibility
            // since it will be caching the assembly when using the same name
            // and use the old one instead of the new one
            var tmpFile = Path.Combine(FilePaths.ProgramsFolder, Guid.NewGuid() + ".dll");
            var result = new CompilerResults(null);
            try
            {
                result = CSharpAppFactory.CompileScript(ProgramBlock.ScriptCondition, ProgramBlock.ScriptSource, tmpFile);
            }
            catch (Exception ex)
            {
                // report errors during post-compilation process
                result.Errors.Add(new CompilerError(ProgramBlock.Name, 0, 0, "-1", ex.Message));
            }

            if (result.Errors.Count > 0)
            {
                var sourceLines = ProgramBlock.ScriptSource.Split('\n').Length;
                foreach (CompilerError error in result.Errors)
                {
                    var errorRow = (error.Line - CSharpAppFactory.ProgramCodeOffset);
                    var blockType = CodeBlockEnum.CR;
                    if (errorRow >= sourceLines + CSharpAppFactory.ConditionCodeOffset)
                    {
                        errorRow -= (sourceLines + CSharpAppFactory.ConditionCodeOffset);
                        blockType = CodeBlockEnum.TC;
                    }
                    if (!error.IsWarning)
                    {
                        errors.Add(new ProgramError {
                            Line = errorRow,
                            Column = error.Column,
                            ErrorMessage = error.ErrorText,
                            ErrorNumber = error.ErrorNumber,
                            CodeBlock = blockType
                        });
                    }
                    else
                    {
                        var warning = string.Format("{0},{1},{2}: {3}", blockType, errorRow, error.Column, error.ErrorText);
                        Homegenie.ProgramManager.RaiseProgramModuleEvent(ProgramBlock, Properties.CompilerWarning, warning);
                    }
                }
            }

            if (errors.Count != 0)
                return errors;

            // move/copy new assembly files
            // rename temp file to production file
            _scriptAssembly = result.CompiledAssembly;
            try
            {
                //string tmpfile = new Uri(value.CodeBase).LocalPath;
                File.Move(tmpFile, AssemblyFile);
                if (File.Exists(tmpFile + ".mdb"))
                {
                    File.Move(tmpFile + ".mdb", AssemblyFile + ".mdb");
                }
                if (File.Exists(tmpFile.Replace(".dll", ".mdb")))
                {
                    File.Move(tmpFile.Replace(".dll", ".mdb"), AssemblyFile.Replace(".dll", ".mdb"));
                }
                if (File.Exists(tmpFile + ".pdb"))
                {
                    File.Move(tmpFile + ".pdb", AssemblyFile + ".pdb");
                }
                if (File.Exists(tmpFile.Replace(".dll", ".pdb")))
                {
                    File.Move(tmpFile.Replace(".dll", ".pdb"), AssemblyFile.Replace(".dll", ".pdb"));
                }
            }
            catch (Exception ee)
            {
                HomeGenieService.LogError(ee);
            }

            return errors;
        }

        public MethodRunResult EvaluateCondition()
        {
            MethodRunResult result = null;
            if (_scriptAssembly != null && CheckAppInstance())
            {
                result = (MethodRunResult)_methodEvaluateCondition.Invoke(_scriptInstance, null);
                result.ReturnValue = (bool)result.ReturnValue || ProgramBlock.WillRun;
            }
            return result;
        }

        public MethodRunResult Run(string options)
        {
            MethodRunResult result = null;
            if (_scriptAssembly != null && CheckAppInstance())
            {
                result = (MethodRunResult)_methodRun.Invoke(_scriptInstance, new object[1] { options });
            }
            return result;
        }

        public void Reset()
        {
            if (_scriptAssembly != null && _methodReset != null)
            {
                _methodReset.Invoke(_scriptInstance, null);
            }
        }

        public ProgramError GetFormattedError(Exception e, bool isTriggerBlock)
        {
            var error = new ProgramError
            {
                CodeBlock = isTriggerBlock ? CodeBlockEnum.TC : CodeBlockEnum.CR,
                Column = 0,
                Line = 0,
                ErrorNumber = "-1",
                ErrorMessage = e.Message
            };
            var st = new StackTrace(e, true);
            error.Line = st.GetFrame(0).GetFileLineNumber();
            if (isTriggerBlock)
            {
                var sourceLines = ProgramBlock.ScriptSource.Split('\n').Length;
                error.Line -=  (CSharpAppFactory.ConditionCodeOffset + CSharpAppFactory.ProgramCodeOffset + sourceLines);
            }
            else
            {
                error.Line -=  CSharpAppFactory.ProgramCodeOffset;
            }
            return error;
        }

        private string AssemblyFile
        {
            get
            {
                //var file = FilePaths.ProgramsFolder;//Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.ProgramsFolder);
                var file = Path.Combine(FilePaths.ProgramsFolder, ProgramBlock.Address + ".dll");
                return file;
            }
        }

        private bool LoadAssembly()
        {
            if (ProgramBlock.Type.ToLower() != "csharp")
                return false;

            try
            {
                var assemblyData = File.ReadAllBytes(AssemblyFile);
                byte[] debugData = null;
                if (File.Exists(AssemblyFile + ".mdb"))
                {
                    debugData = File.ReadAllBytes(AssemblyFile + ".mdb");
                }
                else if (File.Exists(AssemblyFile + ".pdb"))
                {
                    debugData = File.ReadAllBytes(AssemblyFile + ".pdb");
                }
                _scriptAssembly = debugData != null
                    ? Assembly.Load(assemblyData, debugData)
                    : Assembly.Load(assemblyData);
                return true;
            }
            catch (Exception e)
            {
                ProgramBlock.ScriptErrors = e.Message + "\n" + e.StackTrace;
                return false;
            }
        }

        private bool CheckAppInstance()
        {
            var success = false;
            if (_programDomain != null)
            {
                success = true;
            }
            else
            {
                try
                {
                    // Creating app domain
                    _programDomain = AppDomain.CurrentDomain;

                    _assemblyType = _scriptAssembly.GetType("HomeGenie.Automation.Scripting.ScriptingInstance");
                    _scriptInstance = Activator.CreateInstance(_assemblyType);

                    var miSetHost = _assemblyType.GetMethod("SetHost");
                    miSetHost.Invoke(_scriptInstance, new object[2] { Homegenie, ProgramBlock.Address });

                    _methodRun = _assemblyType.GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    // TODO: v1.1 !!!IMPORTANT!!! the method EvaluateCondition will be renamed to EvaluateStartupCode,
                    // TODO: v1.1 !!!IMPORTANT!!! so if EvaluateCondition is not found look for EvaluateStartupCode method instead
                    _methodEvaluateCondition = _assemblyType.GetMethod("EvaluateCondition", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    _methodReset = _assemblyType.GetMethod("Reset");

                    success = true;
                }
                catch (Exception ex)
                {
                    HomeGenieService.LogError(
                        Domains.HomeAutomation_HomeGenie_Automation,
                        ProgramBlock.Address.ToString(),
                        ex.Message,
                        "Exception.StackTrace",
                        ex.StackTrace
                    );
                }
            }
            return success;
        }
    }
}
