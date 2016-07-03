using Arquitectura_CPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaz
{
    public class Consola
    {
        MainWindow mainWindow;
        private string output;
        public Consola(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
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
            string file = "output-" + DateTime.Now.ToString("yyyyMMddHHmmssffff") + ".txt";
            System.IO.File.WriteAllText(file, output);
            return file;
        }

        public void imprimirProcesador(Procesador proc)
        {
            int id = proc.id;
            Contexto contexto = proc.contextos.First();
            string registro = "";
            for (int i = 0; i < contexto.registro.Length; i++)
            {
                registro += String.Format("R{0}: {1} ", i.ToString("D2"), contexto.registro[i].ToString("D5"));
            }
            mainWindow.escribirRegistro(registro, id);

            var cache = proc.cacheDatos;
            string strCache = "";
            for(int i = 0; i < 4; i++)
            {
                strCache += i.ToString() + ": ";
                for (int j = 0; j < 5; j++)
                {
                    if (j == 4)
                        strCache += "|";
                    strCache += String.Format(" {0} ", cache[i][j].ToString());
                }
                strCache += "\n";
            }
            mainWindow.escribirCache(strCache, id);

            var mem = proc.memoriaPrincipal;
            string strDatos = "";
            for (int i = 0; i < 8; i++)
            {
                strDatos += (i + id*8).ToString("D2") + ": ";
                for (int j = 0; j < 4; j++)
                {
                    strDatos += String.Format(" {0} ", mem[i][j][0].ToString());
                }
                if(i % 2 == 1)
                    strDatos += "\n";
                else
                    strDatos += "     ";
            }
            mainWindow.escribirMemo(strDatos, id);

            var dir = proc.directorio;
            string strDir = "";
            for (int i = 0; i < 8; i++)
            {
                strDir += (i + id * 8).ToString("D2") + ": ";
                for (int j = 0; j < 4; j++)
                {
                    strDir += String.Format(" {0} ", dir[i][j].ToString());
                    if (j == 0)
                        strDir += "|";
                }
                if (i % 2 == 1)
                    strDir += "\n";
                else
                    strDir += "     ";
            }
            mainWindow.escribirDirectorios(strDir, id);

        }

        public void escribirInstruccion(string instruccion)
        {
            mainWindow.escribirInstruccion(instruccion);
        }
    }
}

