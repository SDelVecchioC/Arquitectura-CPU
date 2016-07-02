using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arquitectura_CPU
{
    class HiloPrincipal
    {
        //URL: http://stackoverflow.com/questions/3892734/split-c-sharp-collection-into-equal-parts-maintaining-sort
        public static List<T>[] Partition<T>(List<T> list, int totalPartitions)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            if (totalPartitions < 1)
                throw new ArgumentOutOfRangeException("totalPartitions");

            List<T>[] partitions = new List<T>[totalPartitions];

            int maxSize = (int)Math.Ceiling(list.Count / (double)totalPartitions);
            int k = 0;

            for (int i = 0; i < partitions.Length; i++)
            {
                partitions[i] = new List<T>();
                for (int j = k; j < k + maxSize; j++)
                {
                    if (j >= list.Count)
                        break;
                    partitions[i].Add(list[j]);
                }
                k += maxSize;
            }

            return partitions;
        }

        static void Main(string[] args)
        {
            int cantProcesadores = 3;

            Consola console = new Consola();

            var sync = new Barrier(participantCount: cantProcesadores);
            List<string> programas = new List<string>();

            List<Procesador> procesadores = new List<Procesador>();
            List<Thread> hilos = new List<Thread>();
            
            // Recibe el valor de quantum del usuario
            Console.WriteLine("Por favor indicar el quantum a utilizar");
            string unParsedQuantum = Console.ReadLine();
            int parsedQuantum;
            while (!Int32.TryParse(unParsedQuantum, out parsedQuantum))
            {
                Console.WriteLine("El valor indicado no es numérico, por favor indicar el quantum a utilizar");
                unParsedQuantum = Console.ReadLine();
            } 
                
            // Lee los archivos y los reparte
            foreach (string file in Directory.EnumerateFiles("./programas", "*.txt"))
            {
                string contents = File.ReadAllText(file);
                programas.Add(contents);
            }

            var programasPorCpu = Partition(programas, cantProcesadores);

            // creacion de procesadores
            for (int i = 0; i < cantProcesadores; i++)
            {
                var cpu = new Procesador(i, 2000, sync, programasPorCpu.ElementAt(i), console, parsedQuantum); //pasa por referencia los otros procesadores
                procesadores.Add(cpu);
            }

            for (int i = 0; i < cantProcesadores; i++)
            {
                procesadores.ElementAt(i).setProcesadores(procesadores);
            }

            // creacion de hilos
            for (int i = 0; i < cantProcesadores; i++)
            {
                var hiloCpu = new Thread(procesadores.ElementAt(i).Iniciar);
                hilos.Add(hiloCpu);
                hiloCpu.Start();
            }

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
            Console.ReadLine();
        }

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

    }
}
