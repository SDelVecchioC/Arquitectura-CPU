using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Arquitectura_CPU
{
    class HiloPrincipal
    {
        public static List<List<T>> SplitList<T>(List<T> locations, int nSize)
        {

            var res = new List<List<T>>();
            for(var i = 0; i < nSize; i++)
            {
                var l = new List<T>();
                res.Add(l);
            }
            for(var i = 0; i < locations.Count; i++)
            {
                res.ElementAt(i % nSize).Add(locations.ElementAt(i));
            }
            return res;
        }

        public static void Main(string[] args)
        {
            const int cantProcesadores = 3;

            var console = new Consola();

            var sync = new Barrier(participantCount: cantProcesadores);

            // Recibe el valor de quantum del usuario
            Console.WriteLine("Por favor indicar el quantum a utilizar");
            var unParsedQuantum = Console.ReadLine();
            int parsedQuantum;
            while (!int.TryParse(unParsedQuantum, out parsedQuantum))
            {
                Console.WriteLine("El valor indicado no es numérico, por favor indicar el quantum a utilizar");
                unParsedQuantum = Console.ReadLine();
            }

            Console.WriteLine("Presione\n1: correr hilillos solo LW\n2: correr hilillos sin LL SC pocos ciclos");
            var unParsedOption = Console.ReadLine();
            int parsedOption;
            while (!int.TryParse(unParsedOption, out parsedOption))
            {
                Console.WriteLine("El valor indicado no es numérico, por favor indicar el quantum a utilizar");
                unParsedOption = Console.ReadLine();
            }

            List<string> programas = null;
            switch (parsedOption)
            {
                case 1:
                    programas = Directory.EnumerateFiles("./programas/soloLW", "*.txt").Select(File.ReadAllText).ToList();
                    break;
                case 2:
                    programas = Directory.EnumerateFiles("./programas/sinLLSC", "*.txt").Select(File.ReadAllText).ToList();
                    break;
                case 3:
                    programas = Directory.EnumerateFiles("./programas", "*.txt").Select(File.ReadAllText).ToList();
                    break;
            }

            // Lee los archivos y los reparte
            

            var programasPorCpu = SplitList(programas, cantProcesadores);

            var procesador1 = new Procesador(0, sync, programasPorCpu.ElementAt(0), console, parsedQuantum);
            var procesador2 = new Procesador(1, sync, programasPorCpu.ElementAt(1), console, parsedQuantum);
            var procesador3 = new Procesador(2, sync, programasPorCpu.ElementAt(2), console, parsedQuantum);

            procesador1.SetProcesadores(ref procesador1, ref procesador2, ref procesador3);
            procesador2.SetProcesadores(ref procesador1, ref procesador2, ref procesador3);
            procesador3.SetProcesadores(ref procesador1, ref procesador2, ref procesador3);

            var hiloCpu1 = new Thread(procesador1.Iniciar);
            var hiloCpu2 = new Thread(procesador2.Iniciar);
            var hiloCpu3 = new Thread(procesador3.Iniciar);

            hiloCpu1.Start();
            hiloCpu2.Start();
            hiloCpu3.Start();

            hiloCpu1.Join();
            hiloCpu2.Join();
            hiloCpu3.Join();

            ImprimirResultados(procesador1, console);
            ImprimirResultados(procesador2, console);
            ImprimirResultados(procesador3, console);

            console.WriteLine("Memoria Compartida");
            ImprimirMemoC(procesador1, console);
            ImprimirMemoC(procesador2, console);
            ImprimirMemoC(procesador3, console);

            console.WriteLine($"Puede consultar la salida de este programa en el archivo {console.Guardar()}");
            console.WriteLine("Presione enter para salir");
            Console.ReadLine();
        }

        private static void ImprimirResultados(Procesador p, Consola console)
        {
            foreach (var contexto in p.ContextosFinalizados)
            {
                console.WriteLine($"Resultados del hilillo #{contexto.Id} del procesador #{contexto.IdProc}:");
                console.WriteLine(
                    $"Ciclo Inicial: {contexto.CicloInicial}. Ciclo Final: {contexto.CicloFinal}. Total de ciclos: {contexto.CicloFinal - contexto.CicloInicial}");
                for (var i = 0; i < contexto.Registro.Length; i++)
                {
                    console.Write($"R{i.ToString("D2")}: {contexto.Registro[i].ToString("D5")} ");
                    if (i % 5 == 4)
                    {
                        console.WriteLine("");
                    }
                }
                console.WriteLine("");
            }
        }
        private static void ImprimirMemoC(Procesador p, Consola console)
        { 
            for (var i = 0; i < 8; i++)
            {
                console.Write($"Bloque #{i + (p.Id*8)} ");
                for (var j = 0; j < 4; j++)
                {
                    console.Write($"{p.MemoriaPrincipal[i][j][0]} ");
                }
                if (i % 4 == 3)
                {
                    console.WriteLine("");
                }
               // console.WriteLine("");
            }
        }

    }
}
