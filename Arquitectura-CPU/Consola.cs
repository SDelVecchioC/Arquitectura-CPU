using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            output += str + "\n";
        }

        public void Write(String str)
        {
            Console.Write(str);
            output += str + "\n";
        }

        public string Guardar()
        {
            string file = "output-"+DateTime.Now.ToString("yyyyMMddHHmmssffff")+".txt";
            System.IO.File.WriteAllText(file, output);
            return file;
        }
    }
}
