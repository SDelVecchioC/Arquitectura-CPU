using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arquitectura_CPU
{
    public class HiloPrincipal
    {
        int cantProcesadores, quantum;

        public HiloPrincipal(int q)
        {
            quantum = q;
            cantProcesadores = 3;
        }
        /*
        public static List<List<T>> SplitList<T>(List<T> locations, int nSize)
        {
            List<List<T>> res = new List<List<T>>();
            for(int i = 0; i < nSize; i++)
            {
                var l = new List<T>();
                res.Add(l);
            }
            for(int i = 0; i < locations.Count; i++)
            {
                res.ElementAt(i % nSize).Add(locations.ElementAt(i));
            }
            return res;
        }

        public void Iniciar()
        {   
            var sync = new Barrier(participantCount: cantProcesadores);
            List<string> programas = new List<string>();

            List<Procesador> procesadores = new List<Procesador>();
            List<Thread> hilos = new List<Thread>();
                  
            // Lee los archivos y los reparte
            foreach (string file in Directory.EnumerateFiles("./programas", "*.txt"))
            {
                string contents = File.ReadAllText(file);
                programas.Add(contents);
            }

            List<List<string>> programasPorCpu = SplitList(programas, cantProcesadores);

            // creacion de procesadores
            for (int i = 0; i < cantProcesadores; i++)
            {
                var cpu = new Procesador(i, 2000, sync, programasPorCpu.ElementAt(i), consola, quantum);
                procesadores.Add(cpu);
            }

            // creacion de hilos
            for (int i = 0; i < cantProcesadores; i++)
            {
                var proc = procesadores.ElementAt(i);
                proc.setProcesadores(procesadores);
                var hiloCpu = new Thread(proc.Iniciar);
                hilos.Add(hiloCpu);
                //hiloCpu.Start();
                consola.imprimirProcesador(proc);
            }
            /*
            for (int i = 0; i < cantProcesadores; i++)
            {
                hilos.ElementAt(i).Join();
            }

            for (int i = 0; i < cantProcesadores; i++)
            {
                imprimirResultados(procesadores.ElementAt(i), console);
            }
            console.WriteLine("Memoria Compartida");
            for (int i = 0; i < cantProcesadores; i++)
            {
                imprimirMemoC(procesadores.ElementAt(i), console);
            }

            console.WriteLine(String.Format("Puede consultar la salida de este programa en el archivo {0}", console.Guardar()));
            console.WriteLine("Presione una tecla para salir");
            Console.ReadLine();*/
        //}
    
        static void Main()
        {

        }
        /*
        private static void imprimirResultados(Procesador p, Consola console)
        {
            foreach (var contexto in p.contextosFinalizados)
            {
                console.WriteLine(String.Format("Resultados del hilillo #{0} del procesador #{1}:", contexto.id, contexto.idProc));
                console.WriteLine(String.Format("Ciclo Inicial: {0}. Ciclo Final: {1}. Total de ciclos: {2}", contexto.cicloInicial, contexto.cicloFinal, contexto.cicloFinal - contexto.cicloInicial));
                for (int i = 0; i < contexto.registro.Length; i++)
                {
                    console.Write(String.Format("R{0}: {1} ", i.ToString("D2"), contexto.registro[i].ToString("D5")));
                    if (i % 4 == 3)
                    {
                        console.WriteLine("");
                    }
                }
                console.WriteLine("");
            }
        }
        private static void imprimirMemoC(Procesador p, Consola console)
        { 
            for (int i = 0; i < 8; i++)
            {
                console.WriteLine(String.Format("Bloque #{0}", i+(p.id*8)));
                for (int j = 0; j < 4; j++)
                {
                    console.Write(String.Format("{0} ", p.memoriaPrincipal[i][j][0]));
                }
                console.WriteLine("");
            }
        }
        */
    }
}
