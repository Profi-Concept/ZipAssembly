// Copyright (c) 2018, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace System.Reflection
{
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;

    /// <summary>
    /// Load assemblies from a zip file.
    /// </summary>
    public sealed class ZipAssembly : Assembly
    {
        // always set to Zip file full path + \\ + file path in zip.
        private string locationValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipAssembly"/> class.
        /// Load assemblies from a zip file.
        /// </summary>
        public ZipAssembly()
        {
        }

        /// <summary>
        /// Gets the Assembly associated with this ZipAssembly instance.
        /// </summary>
        public Assembly Assembly { get; private set; }

        // hopefully this has the path to the assembly on System.Reflection.Assembly.Location output with the value from this override.

        /// <summary>
        /// Gets the location of the assembly in the zip file.
        /// </summary>
        public override string Location => locationValue;

        /// <summary>
        /// Loads the assembly with it’s debugging symbols
        /// from the specified zip file.
        /// </summary>
        /// <param name="zipFileName">zipFileName</param>
        /// <param name="assemblyName">assemplyName</param>
        /// <param name="loadPDBFile">loadPDBFile</param>
        /// <returns>ZipAssembly</returns>
        public static ZipAssembly LoadFromZip(string zipFileName, string assemblyName, bool loadPDBFile = false)
        {
            if (string.IsNullOrWhiteSpace(zipFileName))
            {
                throw new ArgumentException($"{nameof(zipFileName)} is not allowed to be empty.", nameof(zipFileName));
            }
            else if (!File.Exists(zipFileName))
            {
                throw new ArgumentException($"{nameof(zipFileName)} does not exist.", nameof(zipFileName));
            }

            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                throw new ArgumentException($"{nameof(assemblyName)} is not allowed to be empty.", nameof(assemblyName));
            }
            else if (!assemblyName.EndsWith(".dll"))
            {
                // setting pdbFileName fails or makes unpredicted/unwanted things if this is not checked
                throw new ArgumentException($"{nameof(assemblyName)} must end with '.dll' to be a valid assembly name.", nameof(assemblyName));
            }

            // check if the assembly is in the zip file.
            // If it is, get it’s bytes then load it.
            // If not throw an exception. Also throw
            // an exception if the pdb file is requested but not found.
            bool found = false;
            bool pdbfound = false;
            byte[] asmbytes = null;
            byte[] pdbbytes = null;
            using (ZipArchive zipFile = ZipFile.OpenRead(zipFileName))
            {
                (byte[] bytes, bool found) asm = GetBytesFromZipFile(assemblyName, zipFile);
                asmbytes = asm.bytes;
                found = asm.found;

                if (loadPDBFile || Debugger.IsAttached)
                {
                    string pdbFileName = assemblyName.Replace("dll", "pdb");
                    (byte[] bytes, bool found) pdb = GetBytesFromZipFile(pdbFileName, zipFile);
                    pdbbytes = pdb.bytes;
                    pdbfound = pdb.found;
                }

                zipFile.Dispose(); // With using pattern redundent but good to indicate a dispose
            }

            if (!found)
            {
                throw new ZipAssemblyLoadException(
                    "Assembly specified to load in ZipFile not found.");
            }

            // should only be evaluated if pdb-file is asked for
            if (loadPDBFile && !pdbfound)
            {
                throw new ZipSymbolsLoadException(
                    "pdb to Assembly specified to load in ZipFile not found.");
            }

            // always load pdb when debugging.
            // PDB should be automatically downloaded to zip file always
            // and really *should* always be present.
            bool loadPDB = loadPDBFile ? loadPDBFile : Debugger.IsAttached;
            ZipAssembly zipassembly = new ZipAssembly
            {
                locationValue = zipFileName + Path.DirectorySeparatorChar + assemblyName,
                Assembly = loadPDB ? Assembly.Load(asmbytes, pdbbytes) : Assembly.Load(asmbytes),
            };
            return zipassembly;
        }

        private static (byte[] bytes, bool found) GetBytesFromZipFile(string entryName, ZipArchive zipFile)
        {
            byte[] bytes = null;
            bool found = false;
            ZipArchiveEntry assemblyEntry = zipFile.Entries.Where(e => e.FullName.Equals(entryName)).FirstOrDefault();

            if (assemblyEntry != null)
            {
                found = true;
                using (Stream strm = assemblyEntry.Open())
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        strm.CopyTo(ms);
                        bytes = ms.ToArray();
                        ms.Dispose();
                    }

                    strm.Dispose();
                }
            }

            return (bytes, found);
        }
    }
}
