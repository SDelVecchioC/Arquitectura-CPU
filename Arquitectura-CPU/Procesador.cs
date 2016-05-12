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
        public int[][] cacheInstrucciones;
        public int[][] memoriaPrincipal;
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

            cacheInstrucciones = new int[4][];
            for (int i = 0; i < 4; i++)
            {
                blockMap[i] = -1;
                cacheInstrucciones[i] = new int[4];
                for (int j = 0; j < 4; j++)
                {
                    cacheInstrucciones[i][j] = 0;
                }
            }

            memoriaPrincipal = new int[16][];
            for (int i = 0; i < 16; i++)
            {
                memoriaPrincipal[i] = new int[4];
                for (int j = 0; j < 4; j++)
                    memoriaPrincipal[i][j] = 0;
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
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] + regDest;  
                    break;
                case 32:
                    /*
                        DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                        CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                        */
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] + contPrincipal.registro[regDest];
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] - contPrincipal.registro[regDest];
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] * contPrincipal.registro[regDest];
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    contPrincipal.registro[regFuente1] = contPrincipal.registro[regFuente2] / contPrincipal.registro[regDest];
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
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
                    contPrincipal.registro[31] = contPrincipal.pc;
                    contPrincipal.pc += regDest;
                    break;
                case 2:
                    /*
                    JR RX: PC=RX
                    CodOp: 2 RF1: X RF2 O RD: 0 RD o IMM:0
                    */
                    contPrincipal.pc = contPrincipal.registro[regFuente1];
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    res = true;
                    break;

            }
            return res;
        }

        public void manejoArchivo(List<string> programas)
        {
            int direccionRam = 128;

            foreach(var p in programas)
            {
                // le quito los cambios de linea y que queden separados por espacios
                var programa = p.Replace(System.Environment.NewLine, " ").Trim();

                // los separo por coma
                string[] n = programa.Split(' ');

                // los convierto a int
                int[] numeros = Array.ConvertAll(n, int.Parse);

                Contexto contexto = new Contexto(direccionRam);
                // cargar en la RAM
                foreach(int numero in numeros)
                {
                    var direccion = getPosicion(direccionRam);
                    memoriaPrincipal[direccion.Item1][direccion.Item2] = numero;
                    direccionRam++;
                }
                contextos.Add(contexto);
            }
  
        }

        private Tuple<int, int> getPosicion(int direccion)
        {
            int bloque = (int)direccion / 16;
            int posicion = direccion % 4;
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
                        for (int i = 0; i < 4; i++)
                            cacheInstrucciones[posicion.Item1 % 4][i] = memoriaPrincipal[posicion.Item1][i];
                        blockMap[posicion.Item1 % 4] = posicion.Item1;

                        falloCache = true;
                        ciclosEnFallo = 16;
                        Console.WriteLine("[{0}] Fallo de cache, ciclo: {1}", id, cicloActual);
                    }
                    else
                    {
                        /// @TODO Ejecutar mofo
                        Console.WriteLine("[{0}] {1} {2} {3} {4}, ciclo: {5}", id, cacheInstrucciones[posicion.Item1 % 4][0], cacheInstrucciones[posicion.Item1 % 4][1], cacheInstrucciones[posicion.Item1 % 4][2], cacheInstrucciones[posicion.Item1 % 4][3], cicloActual);
                        bool res = manejoInstrucciones(cacheInstrucciones[posicion.Item1 % 4]);
                        if(res)
                        {
                            Console.WriteLine("[{0}] Murio hile, ciclo: {1}", id, cicloActual);
                            contextos.RemoveAt(0);// @TODO controlar out of bounds
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
