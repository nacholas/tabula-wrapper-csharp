using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TabulaWrapper
{
    public class Tabula
    {
        private string executable;

        public Tabula()
        {
            checkRequirements();
            unpackTabula();
        }

        public DataTable ParseDocument(string file, string pages="all", bool guess=false, 
            bool lattice=false, bool stream=false, string area="", string columns="")
        {
            List<string> argList = new List<string>();
            argList.Add($"\"{file}\"");
            argList.Add($"--pages {pages}");
            argList.Add($"--format JSON");
            if (lattice){
                argList.Add($"--lattice");
            }
            if (stream){
                argList.Add($"--stream");
            }
            if(!String.IsNullOrEmpty(area)){
                argList.Add($"--area \"{area}\"");
            } else if (guess) {
                argList.Add($"--guess");
            }
            if (!String.IsNullOrEmpty(columns)){
                argList.Add($"--columns \"{columns}\"");
            }

            string output = runTabula(argList.ToArray());
            return processJsonOutput(output);
        }
        
        //very basic implementation, not sure if this works for all tables
        private DataTable processJsonOutput(string output)
        {
            JsonDocument jsonDocument = JsonDocument.Parse(output);
            JsonElement root = jsonDocument.RootElement;

            DataTable table = new DataTable();

            for(int i = 0; i < root.GetArrayLength(); i++)
            {
                JsonElement data = root[i].GetProperty("data");
                if(data.ValueKind != JsonValueKind.Array)
                {
                    throw new Exception("Expected array!");
                }
                for (int j = 0; j < data.GetArrayLength(); j++)
                {
                    JsonElement dataElement = data[j];
                    if (dataElement.ValueKind != JsonValueKind.Array)
                    {
                        throw new Exception("Expected array!");
                    }

                    //init datatable columns on first execution
                    if (table.Columns.Count == 0)
                    {
                        for (int k = 0; k < dataElement.GetArrayLength(); k++)
                        {
                            table.Columns.Add();
                            table.Columns[k].DataType = typeof(string);
                        }
                    }

                    //fill DataTable
                    var r = table.NewRow();
                    for (int k = 0; k < data[0].GetArrayLength(); k++)
                    {
                        if (data[0][k].ValueKind != JsonValueKind.Object)
                        {
                            throw new Exception("Expected object!");
                        }
                        r[k] = data[0][k].GetProperty("text");
                    }
                    table.Rows.Add(r);
                }
            }
            return table;
        }

        private void checkRequirements()
        {
            string versionInfo = detectJava();
            if(String.IsNullOrEmpty(versionInfo))
            {
                throw new Exception("Could not find java! Please make sure it's in $PATH.");
            }
        }

        private string runTabula(string[] arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "java";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.StandardOutputEncoding = Encoding.UTF8;

            string tabula_args = String.Join(" ", arguments);
            psi.Arguments = $"-Dfile.encoding=UTF8 -jar \"{executable}\" " + tabula_args;
            Process p = Process.Start(psi);
            string strOutput = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return strOutput;
        }

        private string detectJava()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "java";
            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;

            psi.Arguments = "-version";
            Process p = Process.Start(psi);
            string strOutput = p.StandardError.ReadToEnd();
            p.WaitForExit();

            return strOutput;
        }

        private void unpackTabula()
        {
            string temp = Path.GetTempPath();
            string suffix = "tabula-csharp";
            string dir = Path.Combine(temp, suffix);
            Directory.CreateDirectory(dir);

            string file = "tabula-1.0.3-jar-with-dependencies.jar";
            string targetPath = Path.Combine(dir, file);
            if (!File.Exists(targetPath))
            {
                using (var f = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                {
                    f.Write(TabulaWrapper.Properties.Resources.tabula_1_0_3_jar_with_dependencies);
                }
            }

            this.executable = targetPath;
        }
    }
}
