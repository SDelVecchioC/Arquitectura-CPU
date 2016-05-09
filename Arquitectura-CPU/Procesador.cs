using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arquitectura_CPU
{
    class Procesador
    {
        // estructuras de datos del procesador
        public int[,] cacheInstrucciones = new int[4, 4];
        public int[,] memoriaPrincipal = new int[16, 4];
        public int[] blockMap = new int[4];
        public int[] registros = new int[32];

        // referente a sincronizacion
        public Barrier sync;
        public int id, cicloActual, maxCiclo;
        public int pc;
        public Contexto contextoPrincipal;
        public Contexto[] contextos;



        public Procesador(int id, int maxCiclo, Barrier s)
        {
            this.sync = s;
            this.id = id;
            cicloActual = 0;
            this.maxCiclo = maxCiclo;

            for (int i = 0; i < 4; i++)
            {
                blockMap[i] = -1;
                for (int j = 0; j < 4; j++)
                {
                    cacheInstrucciones[i, j] = 0;
                }
            }

            for (int i = 0; i < 16; i++)
                for (int j = 0; j < 4; j++)
                    memoriaPrincipal[i, j] = 0;

        }

        public void manejoInstrucciones(int codigoInstruccion, int regFuente1, int regFuente2, int regDest) //el regDest puede ser un inmediato
        {

            switch (codigoInstruccion)
            {
                case 8:
                    /*
                    DADDI RX, RY, #n : Rx <-- (Ry) + n
                    CodOp: 8 RF1: Y RF2 O RD: x RD O IMM:n
                    */
                    registros[regFuente1] = registros[regFuente2] + regDest;
                    break;
                case 32:
                    /*
                        DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                        CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                        */
                    registros[regFuente1] = registros[regFuente2] + registros[regDest];
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    registros[regFuente1] = registros[regFuente2] - registros[regDest];
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    registros[regFuente1] = registros[regFuente2] * registros[regDest];
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    registros[regFuente1] = registros[regFuente2] / registros[regDest];
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
                    if (regFuente1 == 0)
                    {
                        //salta a la etiqueta indicada por regFuente2
                    }
                    break;
                case 5:
                    /*
                     BEQNZ RX, ETIQ : Si RX != 0 salta 
                     CodOp: 5 RF1: x RF2 O RD: 0 RD o IMM:n
                     */
                    if (regFuente1 != 0)
                    {
                        //salta a la etiqueta indicada por regFuente2
                    }
                    break;
                case 3:
                    /*
                    JAL n, R31=PC, PC = PC+n
                    CodOp: 3 RF1: 0 RF2 O RD: 0 RD o IMM:n
                    */
                    registros[30] = pc;
                    pc += regDest;
                    break;
                case 2:
                    /*
                    JR RX: PC=RX
                    CodOp: 2 RF1: X RF2 O RD: 0 RD o IMM:0
                    */
                    pc = registros[regFuente1];
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    break;

            }
        }

        public void manejoArchivo(string nombre)
        {
            int contador = 0;
            string line;


            // Read the file and display it line by line.
            System.IO.StreamReader file =
            new System.IO.StreamReader(nombre);
            while ((line = file.ReadLine()) != null)
            {
                //Console.WriteLine (line);
                int ultimaPos = 0;
                int largoSubStr = 0;
                int[] instEntrada = new int[4];
                int numParam = 0;
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == ' ')
                    {
                        //extrae los números de manera individual 
                        instEntrada[numParam] = Convert.ToInt32(line.Substring(ultimaPos, largoSubStr));
                        numParam++;
                        ultimaPos = i++;
                        largoSubStr = 0;

                    }
                    else
                        largoSubStr++;
                }
                manejoInstrucciones(instEntrada[0], instEntrada[1], instEntrada[2], instEntrada[4]
                );
                contador++;
            }

        }


        public void Iniciar()
        {
            while (cicloActual < maxCiclo)
            {
                // Need to sync here
                sync.SignalAndWait();

                // Perform some more work
                Console.WriteLine("[{0}] Ejecuto el ciclo: {1}", id, cicloActual);

                Random r = new Random();
                TimeSpan t = TimeSpan.FromSeconds(r.Next(3));
                Thread.Sleep(t);

                cicloActual++;
            }
        }
    }
}
