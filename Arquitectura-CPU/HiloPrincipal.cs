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


            var sync = new Barrier(participantCount: cantProcesadores);
            List<string> programas = new List<string>();

            List<Procesador> procesadores = new List<Procesador>();
            List<Thread> hilos = new List<Thread>();


            // leer los archivos y repartirlos
            foreach (string file in Directory.EnumerateFiles("./programas", "*.txt"))
            {
                string contents = File.ReadAllText(file);
                programas.Add(contents);
            }

            var programasPorCpu = Partition(programas, cantProcesadores);

            for(int i = 0; i < cantProcesadores; i++)
            {
                var cpu = new Procesador(i+1, 2000, sync, programasPorCpu.ElementAt(i));
                var hiloCpu = new Thread(cpu.Iniciar);

                procesadores.Add(cpu);
                hilos.Add(hiloCpu);

                hiloCpu.Start();
          
            }

            for (int i = 0; i < cantProcesadores; i++)
            {
                hilos.ElementAt(i).Join();
            }

            for (int i = 0; i < cantProcesadores; i++)
            {
                imprimirResultados(procesadores.ElementAt(i));
            }


            Console.ReadLine();
        }

        private static void imprimirResultados(Procesador p)
        {
            Console.WriteLine("Resultados del procesador {0}:", p.id);
            foreach (var contexto in p.contextosFinalizados)
            {
                Console.WriteLine("Resultados del hilillo {0}:", contexto.id);
                for (int i = 0; i < contexto.registro.Length; i++)
                {

                    Console.Write("R{0}: {1} ", i.ToString("D2"), contexto.registro[i].ToString("D5"));
                    if (i % 4 == 3)
                    {
                        Console.WriteLine("");
                    }
                }
                Console.WriteLine("");
            }

        }

    }
}
