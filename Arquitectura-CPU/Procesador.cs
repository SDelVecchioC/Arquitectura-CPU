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
        public int[][][] cacheInstrucciones;
        public int[][][] memoriaPrincipal;
        public int[] blockMap = new int[4];
        bool falloCache;
        int ciclosEnFallo;

        // referente a sincronizacion
        public Barrier sync;
        public int id, cicloActual, maxCiclo;
        public int quantum;
        public List<Contexto> contextos;



        public Procesador(int id, int maxCiclo, Barrier s, List<string> programas)
        {
            this.sync = s;
            this.id = id;
            cicloActual = 0;
            this.maxCiclo = maxCiclo;
            falloCache = false;
            ciclosEnFallo = 0;
            // TODO recibir de usuario
            quantum = 30;

            cacheInstrucciones = new int[4][][];
            for (int i = 0; i < 4; i++)
            {
                blockMap[i] = -1;
                cacheInstrucciones[i] = new int[4][];
                for (int j = 0; j < 4; j++)
                {
                    cacheInstrucciones[i][j] = new int[4];
                    for (int k = 0; k < 4; k++)
                    {
                        cacheInstrucciones[i][j][k] = 0;
                    }
                }
            }

            memoriaPrincipal = new int[16][][];
            for (int i = 0; i < 16; i++)
            {
                memoriaPrincipal[i] = new int[4][];
                for (int j = 0; j < 4; j++)
                {
                    memoriaPrincipal[i][j] = new int[4];
                    for (int k = 0; k < 4; k++)
                    {
                        memoriaPrincipal[i][j][k] = 0;
                    }
                }
            }

            contextos = new List<Contexto>();
            manejoArchivo(programas);

        }

        public static void ShiftLeft<T>(List<T> lst, int shifts)
        {
            for (int i = 0; i < shifts; i++)
            {
                lst.Add(lst.ElementAt(0));
                lst.RemoveAt(0);
            }
        }

        public bool manejoInstrucciones(int[] instruccion) //el regDest puede ser un inmediato
        {
            bool res = false;
            int codigoInstruccion = instruccion[0],
                regFuente1 = instruccion[1],
                regFuente2 = instruccion[2],
                regDest = instruccion[3];

            Contexto contPrincipal = contextos.ElementAt(0);

            contPrincipal.pc += 4;
            switch (codigoInstruccion)
            {
                case 8:
                    /*
                    DADDI RX, RY, #n : Rx <-- (Ry) + n
                    CodOp: 8 RF1: Y RF2 O RD: x RD O IMM:n
                    */
                    Console.WriteLine("DADDI R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2],regDest);
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] + regDest;
                    break;
                case 32:
                    /*
                        DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                        CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                        */
                    Console.WriteLine("DADD R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] + contPrincipal.registro[regDest];
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    Console.WriteLine("DSUB R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] - contPrincipal.registro[regDest];
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    Console.WriteLine("DMUL R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] * contPrincipal.registro[regDest];
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    Console.WriteLine("DDIV R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] / contPrincipal.registro[regDest];
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
                    Console.WriteLine("BEQZ R{0},{1}", contPrincipal.registro[regFuente1], regDest);
                    if (contPrincipal.registro[regFuente1] == 0)
                    {
                        contPrincipal.pc += (regDest << 2);
                        //salta a la etiqueta indicada por regDest
                    }
                    break;
                case 5:
                    /*
                     BEQNZ RX, ETIQ : Si RX != 0 salta 
                     CodOp: 5 RF1: x RF2 O RD: 0 RD o IMM:n
                     */
                    Console.WriteLine("BEQNZ R{0},{1}", contPrincipal.registro[regFuente1], regDest);
                    if (contPrincipal.registro[regFuente1] != 0)
                    {
                        //salta a la etiqueta indicada por regDest
                        contPrincipal.pc += (regDest << 2);
                    }
                    break;
                case 3:
                    /*
                    JAL n, R31=PC, PC = PC+n
                    CodOp: 3 RF1: 0 RF2 O RD: 0 RD o IMM:n
                    */
                    Console.WriteLine("JAL {0}", regDest);
                    contPrincipal.registro[31] = contPrincipal.pc;
                    contPrincipal.pc += regDest;
                    break;
                case 2:
                    /*
                    JR RX: PC=RX
                    CodOp: 2 RF1: X RF2 O RD: 0 RD o IMM:0
                    */
                    Console.WriteLine("JR RX{0}", contPrincipal.registro[regFuente1]);
                    contPrincipal.pc = contPrincipal.registro[regFuente1];
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    Console.WriteLine("FIN");
                    res = true;
                    break;

            }
            return res;
        }

        public void manejoArchivo(List<string> programas)
        {
            int direccionRam = 128;

            foreach (var p in programas)
            {
                // para cada programa

                // cada linea es una instruccion de 4 numeros
                string[] instrucciones = p.Split('\n');

                Contexto contexto = new Contexto(direccionRam);
                contextos.Add(contexto);

                foreach (var i in instrucciones)
                {
                    // para cada instruccion separo los 4 numeros
                    string[] instruccion = i.Split(' ');
                    int[] numeros = Array.ConvertAll(instruccion, int.Parse);

                    for (int m = 0; m < 4; m++)
                    {
                        var direccion = getPosicion(direccionRam);
                        memoriaPrincipal[direccion.Item1][direccion.Item2][m] = numeros[m];
                        Console.WriteLine("Memoria Principal[{0}][{1}][{2}]=[{3}])", direccion.Item1, direccion.Item2, m, memoriaPrincipal[direccion.Item1][direccion.Item2][m]);
                    }
                    direccionRam += 4;
                }
            }
        }

        private Tuple<int, int> getPosicion(int direccion)
        {
            int bloque = (int)direccion / 16;
            int posicion = (direccion % 16) / 4;
            return new Tuple<int, int>(bloque, posicion);
        }


        public void Iniciar()
        {
            while (contextos.Count > 0)
            {
                // Need to sync here
                sync.SignalAndWait();

                if (!falloCache)
                {

                    // Perform some more work

                    int pc = contextos.ElementAt(0).pc;
                    Tuple<int, int> posicion = getPosicion(pc);
                    if (blockMap[posicion.Item1 % 4] != posicion.Item1)
                    {
                        // Fallo de caché 
                        /// @TODO arreglar para la nueve dimension
                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {

                                cacheInstrucciones[posicion.Item1 % 4][posicion.Item2+j][i] = memoriaPrincipal[posicion.Item1][posicion.Item2+j][i];
                                Console.WriteLine("[{0}] Cache[{1}],[{2}],[{3}]=[{4}]", id, posicion.Item1 % 4, posicion.Item2+j, i, cacheInstrucciones[posicion.Item1 % 4][posicion.Item2+j][i]);
                            }
                        }
                        blockMap[posicion.Item1 % 4] = posicion.Item1;

                        falloCache = true;
                        ciclosEnFallo = 16;
                        Console.WriteLine("[{0}] Fallo de cache, ciclo: {1}", id, cicloActual);
                    }
                    else
                    {
                        /// @TODO Ejecutar mofo
                        Console.WriteLine("[{0}] {1} {2} {3} {4}, ciclo: {5}", id, cacheInstrucciones[posicion.Item1 % 4][posicion.Item2][0], cacheInstrucciones[posicion.Item1 % 4][posicion.Item2][1], cacheInstrucciones[posicion.Item1 % 4][posicion.Item2][2], cacheInstrucciones[posicion.Item1 % 4][posicion.Item2][3], cicloActual);
                        bool res = manejoInstrucciones(cacheInstrucciones[posicion.Item1 % 4][posicion.Item2]);
                        if(res)
                        {
                            Console.WriteLine("[{0}] Murio hile, ciclo: {1}", id, cicloActual);
                            contextos.RemoveAt(0);// @TODO contmanejorolar out of bounds
                        }
                        else
                        {
                            quantum--;
                            if (quantum == 0)
                            {
                                // Hacer cambio de contexto!
                                ShiftLeft(contextos, 1);
                                Console.WriteLine("[{0}] Cambio contexto, ciclo: {1}", id, cicloActual);
                                quantum = 30;
                            }
                        }
                        
                    }

                }
                else
                {
                    // si hay fallo de cache, el quantum no avanza
                    if (ciclosEnFallo == 0)
                    {
                        falloCache = false;
                    }
                    else
                    {
                        ciclosEnFallo--;
                    }
                }
                cicloActual++;
            }
        }
    }
}
