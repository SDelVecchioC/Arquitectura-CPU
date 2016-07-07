using System;

namespace Arquitectura_CPU
{
    class Consola
    {
        private string output;
        public Consola()
        {
            output = "";
        }

        public void WriteLine(String str)
        {
            Console.WriteLine(str);
            output += str + Environment.NewLine;
        }

        public void Write(String str)
        {
            Console.Write(str);
            output += str + Environment.NewLine;
        }

        public string Guardar()
        {
            string file = "output-"+DateTime.Now.ToString("yyyyMMddHHmmssffff")+".txt";
            System.IO.File.WriteAllText(file, output);
            return file;
        }
    }
}
