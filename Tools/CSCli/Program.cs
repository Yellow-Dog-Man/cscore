﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Pdb;

namespace CSCli
{
    public class Program
    {
        private static void Main(string[] args)
        {
            string calliAttributeName = "CalliAttribute";
            string removeTypeAttributeName = "RemoveTypeAttribute";
            string filename = null;
            string pdbfile = null;

            MessageIntegration.Info("CSCli started.");
            MessageIntegration.Info("CSCli was copied from http://www.codeproject.com/Articles/644130/NET-COM-Interop-using-Postbuild.");
            MessageIntegration.Info(String.Empty); //new line

            MessageIntegration.Info(String.Format("CSCli-Argments: [{0}].", String.Concat(args.Select(x => x + " ")).Trim()));

            /*
             * Parse parameters
             */
            foreach (var a in args)
            {
                if (a.StartsWith("-file:"))
                {
                    filename = a.Substring("-file:".Length);
                    MessageIntegration.Info("Filename: " + filename);
                }
                else if (a.StartsWith("-c:"))
                {
                    calliAttributeName = a.Substring("-c:".Length);
                }
                else if (a.StartsWith("-r:"))
                {
                    removeTypeAttributeName = a.Substring("-r:".Length);
                }
                else if (a.StartsWith("-pdb:"))
                {
                    pdbfile = a.Substring("-pdb:".Length);
                }
                else
                {
                    MessageIntegration.WriteWarning(String.Format("Unknown parameter: \"{0}\".", a));
                }
            }

            /*
             * Load and process assembly
             */
            if (String.IsNullOrEmpty(filename) || !File.Exists(filename))
            {
                MessageIntegration.WriteError(String.Format("Could not find file \"{0}\".", filename));
                Environment.Exit(-4);
            }
            string originalFilename = filename + "_orig"; //use this filename for reader of Mono.Cecil

            if(File.Exists(originalFilename))
            {
                //delete previous version, if existing 
                File.Delete(originalFilename); 
            }

            try
            {
                File.Move(filename, originalFilename); //file for reader
            }
            catch (Exception ex)
            {
                MessageIntegration.WriteError("Rename file to _orig failed: " + ex);
                Environment.Exit(-3);
            }

            WriterParameters wp = new WriterParameters();
            ReaderParameters rp = new ReaderParameters();

            /*var strongNameKey = Path.ChangeExtension(filename, "snk");
            if (File.Exists(strongNameKey))
            {
                MessageIntegration.Info("Signing with Key : " + strongNameKey);
                wp.StrongNameKeyPair = new StrongNameKeyPair(File.OpenRead(strongNameKey));
            }*/

            //check whether the pdbfile has been passed through application parameters
            if (pdbfile == null)
            {
                //if not use the default pdbfilepath by changing the extension of the assembly to .pdb
                pdbfile = Path.ChangeExtension(filename, "pdb");
            }

            //check whether the original pdb-file exists
            bool generatePdb = File.Exists(pdbfile);

            //if the original pdb-file exists -> prepare for rewriting the symbols file
            wp.WriteSymbols = generatePdb;
            rp.ReadSymbols = generatePdb;

            if (rp.ReadSymbols)
            {
                rp.SymbolReaderProvider = new PdbReaderProvider();
            }

            MessageIntegration.Info("Generating pdb: " + generatePdb);

            //open assembly
            var assembly = AssemblyDefinition.ReadAssembly(originalFilename, rp); //read
            //add the directory assembly directory as search directory to resolve referenced assemblies
            ((BaseAssemblyResolver)assembly.MainModule.AssemblyResolver).AddSearchDirectory(Path.GetDirectoryName(originalFilename));

            //path the assembly
            AssemblyPatcher patcher = new AssemblyPatcher(assembly, calliAttributeName, removeTypeAttributeName);
            if (patcher.PatchAssembly())
            {
                try
                {
                    assembly.Write(filename, wp);
                }
                catch (Exception ex)
                {
                    MessageIntegration.WriteError("Creating patched assembly failed: " + ex);
                    Environment.Exit(-2);
                }

                MessageIntegration.Info(String.Format("CSCli patched assembly \"{0}\" successfully.", Path.GetFileName(filename)));
                Environment.Exit(0);
            }
            else
            {
                MessageIntegration.WriteError(String.Format("\"{0}\" could not be patched.", filename));
                Environment.Exit(-1);
            }

        }

    }
}