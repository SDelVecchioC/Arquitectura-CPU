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
        #region constantes
        // constantes: para salud mental

        public const int CACHDAT_FILAS = 4;
        public const int CACHDAT_COLUMNAS = 5;
        public const int CACHDAT_COL_ESTADO = 4;

        public const int ESTADO_INVALIDO = 0;
        public const int ESTADO_COMPARTIDO = 1;
        public const int ESTADO_MODIFICADO = 2;

        public const int DIRECT_FILAS = 8;
        public const int DIRECT_COLUMNAS = 4;
        public const int DIRECT_COL_ESTADO = 0;

        public const int ESTADO_UNCACHED = 0;
        public const int BLOQUES_COMP = 8;
        #endregion

        /// <summary>
        /// Struct para representar una direccion de memoria
        /// </summary>
        public struct Direccion
        {
            public int numeroBloque;
            public int numeroPalabra;
        }

        // estructuras de datos del procesador
        public int[][][] cacheInstrucciones;
        public int[][][] memoriaPrincipal;
        public int[] blockMap = new int[4];

        private bool estoyEnRetraso;
        private int ciclosEnRetraso;

        // referente a sincronizacion
        public Barrier sync;
        public int id, cicloActual, maxCiclo;
        public int quantum;
        public List<Contexto> contextos, contextosFinalizados;

        // directorio 
        // 8 columnas -> cantidad de bloques memo compart
        // 4 columnas -> 0 estado, 3 cada procesador
        public int[][] directorio;

        // cacheDatos[4][5]
        // 4 filas -> cantidad de bloques de la cache
        // 5 columnas -> 0 para ESTADO, 4 de DATOS
        public int[][] cacheDatos;
        public int[] blockMapDatos;

        private Consola console;

        public List<Procesador> procesadores { get; set; }

        public Procesador(int id, Barrier s, List<string> programas, Consola c, int recievedQuantum)
        {
            console = c;
            this.sync = s;
            this.id = id;
            cicloActual = 1;
            estoyEnRetraso = false;
            ciclosEnRetraso = 0;

            procesadores = new List<Procesador>();

            quantum = recievedQuantum;

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

            directorio = new int[DIRECT_FILAS][];
            for (int j = 0; j < DIRECT_FILAS; j++)
            {
                directorio[j] = new int[DIRECT_COLUMNAS];
                for (int k = 0; k < DIRECT_COLUMNAS; k++)
                {
                    directorio[j][k] = 0;
                }
            }

            cacheDatos = new int[CACHDAT_FILAS][];
            blockMapDatos = new int[CACHDAT_FILAS];

            for (int j = 0; j < CACHDAT_FILAS; j++)
            {
                blockMapDatos[j] = -1;
                cacheDatos[j] = new int[CACHDAT_COLUMNAS];
                for (int k = 0; k < CACHDAT_COLUMNAS; k++)
                {  
                    if (k == CACHDAT_COL_ESTADO)
                    {
                        cacheDatos[j][k] = ESTADO_INVALIDO;
                    }
                    else
                    {
                        cacheDatos[j][k] = -1;
                    }
                }
            }

            memoriaPrincipal = new int[24][][];
            //parte compartida
            for (int i = 0; i < 8; i++)
            {
                memoriaPrincipal[i] = new int[4][];
                for (int j = 0; j < 4; j++)
                {
                    memoriaPrincipal[i][j] = new int[4];
                    for(int k = 0; k < 4; k++)
                    {
                        memoriaPrincipal[i][j][k] = 1;
                    }
                    
                }
            }
            //parte no compartida 
            for (int i = 8; i < 24; i++)
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
            contextosFinalizados = new List<Contexto>();
            manejoArchivo(programas);

        }

        /// <summary>
        /// Recibe y almacena las referencias de los procesadores
        /// </summary>
        /// <param name="p">Lista con los punteros a los procesadores</param>
        public void setProcesadores(ref Procesador p1, ref Procesador p2, ref Procesador p3)
        {
            procesadores.Add(p1);
            procesadores.Add(p2);
            procesadores.Add(p3);
        }

        /// <summary>
        /// Rota una lista a la izquierda de forma circular
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lst"></param>
        /// <param name="shifts"></param>
        public static void ShiftLeft<T>(List<T> lst, int shifts)
        {
            for (int i = 0; i < shifts; i++)
            {
                lst.Add(lst.ElementAt(0));
                lst.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Recibe los programas y los alamcena en memoria principal
        /// </summary>
        /// <param name="programas"></param>
        public void manejoArchivo(List<string> programas)
        {
            int direccionRam = 128;
            int idPrograma = 1;
            foreach (var p in programas)
            {
                // para cada programa
                // cada linea es una instruccion de 4 numeros
                string[] instrucciones = p.Split('\n');

                Contexto contexto = new Contexto(direccionRam, idPrograma, id);
                contextos.Add(contexto);

                foreach (var i in instrucciones)
                {
                    // para cada instruccion separo los 4 numeros
                    string[] instruccion = i.Split(' ');
                    int[] numeros = Array.ConvertAll(instruccion, int.Parse);

                    for (int m = 0; m < 4; m++)
                    {
                        var direccion = getPosicion(direccionRam);
                        memoriaPrincipal[direccion.numeroBloque][direccion.numeroPalabra][m] = numeros[m];
                    }
                    direccionRam += 4;
                }
                idPrograma++;
            }
            contextos.ElementAt(0).cicloInicial = 1;
        }
        
        /// <summary>
        /// Genera la instrucción bonita basada en los códigos de operación
        /// </summary>
        /// <param name="instruccion"></param>
        /// <returns></returns>
        public string getStringInstruccion(int[] instruccion)
        {
            int codigoInstruccion = instruccion[0],
                regFuente1 = instruccion[1],
                regFuente2 = instruccion[2],
                regDest = instruccion[3];
            string res = "";
            Contexto contPrincipal = contextos.ElementAt(0);

            switch (codigoInstruccion)
            {
                case 8:
                    /*
                    DADDI RX, RY, #n : Rx <-- (Ry) + n
                    CodOp: 8 RF1: Y RF2 O RD: x RD O IMM:n
                    */
                    res = String.Format("DADDI R{0},R{1},{2}", regFuente2, regFuente1, regDest);
                    break;
                case 32:
                    /*
                    DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                    CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                    */
                    res = String.Format("DADD R{0},R{1},R{2}", regDest, regFuente1, regFuente2);
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = String.Format("DSUB R{0},R{1},R{2}", regDest, regFuente1, regFuente2);
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = String.Format("DMUL R{0},R{1},R{2}", regDest, regFuente1, regFuente2);
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = String.Format("DDIV R{0},R{1},R{2}", regDest, regFuente1, regFuente2);
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
                    res = String.Format("BEQZ R{0},{1}", regFuente1, regDest);
                    break;
                case 5:
                    /*
                     BEQNZ RX, ETIQ : Si RX != 0 salta 
                     CodOp: 5 RF1: x RF2 O RD: 0 RD o IMM:n
                     */
                    res = String.Format("BNEQZ R{0},{1}", regFuente1, regDest);
                    break;
                case 3:
                    /*
                    JAL n, R31=PC, PC = PC+n
                    CodOp: 3 RF1: 0 RF2 O RD: 0 RD o IMM:n
                    */
                    res = String.Format("JAL {0}", regDest);
                    break;
                case 2:
                    /*
                    JR RX: PC=RX
                    CodOp: 2 RF1: X RF2 O RD: 0 RD o IMM:0
                    */
                    res = String.Format("JR R{0}", regFuente1);
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    res = String.Format("FIN");
                    break;
                case 35:
                    /* *
                    * LW Rx, n(Ry)
                    * Rx <- M(n + (Ry))
                    * 
                    * codOp: 35 RF1: Y RF2 O RD: X RD O IMM: n
                    * */
                    res = String.Format("LW R{0} {1}(R{2})", regFuente2, regDest, regFuente1);
                    break;
                case 50:
                    /* *
                     * LL Rx, n(Ry)
                     * Rx <- M(n + (Ry))
                     * Rl <- n+(Ry)
                     * codOp: 50 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    res = String.Format("LL R{0} {1}(R{2})", regFuente2, regDest, regFuente1);
                    break;
                case 51:
                    /* *
                     * SC RX, n(rY)
                     * IF (rl = N+(Ry)) => m(N+(RY)) = rX
                     * ELSE Rx =0
                     *  codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    res = String.Format("SC R{0} {1}(R{2})", regFuente2, regDest, regFuente1);
                    break;
                case 43:
                    /* *
                     * SW RX, n(rY)
                     * m(N+(RY)) = rX
                     * codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    res = String.Format("SW R{0} {1}(R{2})", regFuente2, regDest, regFuente1);
                    break;
            }
            return res;
        }

        /// <summary>
        /// Procesa las instrucciones
        /// </summary>
        /// <param name="instruccion"></param>
        /// <returns></returns>
        public bool manejoInstrucciones(int[] instruccion)
        {
            bool res = false;
            int codigoInstruccion = instruccion[0],
                regFuente1 = instruccion[1],
                regFuente2 = instruccion[2],
                regDest = instruccion[3],
                posMem = 0;

            Contexto contPrincipal = contextos.ElementAt(0);

            string output = "";
            int pc = contextos.ElementAt(0).pc;
            Direccion posicion = getPosicion(pc);

            switch (codigoInstruccion)
            {
                case 8:
                    /*
                    DADDI RX, RY, #n : Rx <-- (Ry) + n
                    CodOp: 8 RF1: Y RF2 O RD: x RD O IMM:n
                    */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    contPrincipal.registro[regFuente2] = contPrincipal.registro[regFuente1] + regDest;
                    break;
                case 32:
                    /*
                    DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                    CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                    */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] + contPrincipal.registro[regFuente2];
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] - contPrincipal.registro[regFuente2];
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] * contPrincipal.registro[regFuente2];
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] / contPrincipal.registro[regFuente2];
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
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
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
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
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    contPrincipal.registro[31] = contPrincipal.pc;
                    contPrincipal.pc += regDest;
                    break;
                case 2:
                    /*
                    JR RX: PC=RX
                    CodOp: 2 RF1: X RF2 O RD: 0 RD o IMM:0
                    */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    contPrincipal.pc = contPrincipal.registro[regFuente1];
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    res = true;
                    break;
                case 50:
                    /* *
                     * LL Rx, n(Ry)
                     * Rx <- M(n + (Ry))
                     * Rl <- n+(Ry)
                     * codOp: 50 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    posMem = contPrincipal.registro[regFuente1] + regDest;
                    var ll = loadLink(regFuente2, posMem);
                    output += " Pos: " + posMem + " Res: " + ll.ToString();
                    break;
                case 51:
                    /* *
                     * SC RX, n(rY)
                     * IF (rl = N+(Ry)) => m(N+(RY)) = rX
                     * ELSE Rx =0
                     *  codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    posMem = contPrincipal.registro[regFuente1] + regDest;
                    var sc = StoreWord(posMem, regFuente2, true);
                    output += " Pos: " + posMem + " Res: " + sc.ToString();
                    break;
                case 35:
                    /* *
                    * LW Rx, n(Ry)
                    * Rx <- M(n + (Ry))
                    * 
                    * codOp: 35 RF1: Y RF2 O RD: X RD O IMM: n
                    * */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    posMem = contPrincipal.registro[regFuente1] + regDest;
                    int loadRes = LoadWord(regFuente2, posMem);
                    output += " Pos: " + posMem + " Res: " + loadRes.ToString();
                    break;
                case 43:
                    /* *
                     * SW RX, n(rY)
                     * m(N+(RY)) = rX
                     * codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    output = String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]));
                    posMem = contPrincipal.registro[regFuente1] + regDest;
                    int storeRes = StoreWord(posMem, regFuente2);
                    output += " Pos: " + posMem + " Res: " + storeRes.ToString();
                    break;
            }

            console.WriteLine(output);
            /*
            if (output != "")
                console.WriteLine();
            else
                console.WriteLine(String.Format("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra])));
*/
            contPrincipal.pc += 4;

            return res;
        }

        /// <summary>
        /// Calcula el número de procesador que tiene el bloque
        /// </summary>
        /// <param name="numeroBloque"></param>
        /// <returns></returns>
        private int getNumeroProcesador(int numeroBloque)
        {
            return numeroBloque / BLOQUES_COMP;
        }
        
        /// <summary>
        /// Calcula la direccion para un bloque de memoria
        /// </summary>
        /// <param name="direccion"></param>
        /// <returns></returns>
        private Direccion getPosicion(int direccion)
        {
            int bloque = direccion / 16;
            int palabra = (direccion % 16) / 4;
            Direccion d;
            d.numeroBloque = bloque;
            d.numeroPalabra = palabra;
            return d;
        }

        /// <summary>
        /// Determina si un bloque está en la caché de datos del procesador
        /// </summary>
        /// <param name="direccion"></param>
        /// <returns>True si el bloque está en caché</returns>
        public bool bloqueEnMiCache(int numeroBloque)
        {
            return blockMapDatos[numeroBloque % CACHDAT_FILAS] == numeroBloque;
        }

        /// <summary>
        /// Invalida entrada de una caché con bloque Compartida, actualiza entrada directorio
        /// </summary>
        /// <param name="procesadorDirecCasa"></param>
        /// <param name="procAInvalidar"></param>
        /// <param name="direccion"></param>
        private void InvalidarCacheProcesador(Procesador procesadorDirecCasa, Procesador procAInvalidar, Direccion direccion)
        {
            // Invalida caché
            procAInvalidar.cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_INVALIDO;
            procAInvalidar.blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS] = -1;

            // Actualiza el directorio
            procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][procAInvalidar.id + 1] = 0;
        }

        /// <summary>
        /// Busca los procesadores con un bloque en estado compartido, los invalida
        /// y actualiza la entrada del directorio
        /// </summary>
        /// <param name="procesadorDirecCasa"></param>
        /// <param name="direccion"></param>
        /// <returns>True si lo logró invalidar todo correctamente</returns>
        private bool InvalidarCachesCompartidas(Procesador procesadorDirecCasa, Direccion direccion)
        {
            bool exito = true;
            bool bloqueoCache = false;

            /// Busca los procesadores
            List<Procesador> procesadoresTienenComp = new List<Procesador>();
            for (int i = 0; i < 3; i++)
            {
                if (i != id && procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][i + 1] == 1)
                    procesadoresTienenComp.Add(procesadores.ElementAt(i));
            }

            for (int i = 0; i < procesadoresTienenComp.Count && exito; i++)
            {
                Procesador proc = procesadoresTienenComp.ElementAt(i);
                try
                {
                    // Logra bloquear caché
                    bloqueoCache = false;
                    Monitor.TryEnter(proc.cacheDatos, ref bloqueoCache);
                    if (bloqueoCache)
                    {
                        InvalidarCacheProcesador(procesadorDirecCasa, proc, direccion);
                    }
                    else
                    {
                        exito = false;
                    }
                }
                finally
                {
                    if (bloqueoCache)
                        Monitor.Exit(proc.cacheDatos);
                }
            }
            return exito;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="posMem"></param>
        /// <param name="regFuente"></param>
        /// <returns></returns>
        public int StoreWord(int posMem, int regFuente, bool conditional = false)
        {
            // resultado final, sirve para
            // 1. bajar el PC
            // 2. indica si sí se hizo el SW
            int exito = 0;

            // banderas de locks
            bool bloqueoMiCache = false;
            bool bloqueoDirecCasa = false;
            bool bloqueoDirecVictima = false;
            bool bloqueoDirecBloque = false;
            bool bloqueoCacheModif = false;

            // Punteros que hay que liberar
            object objMiCache = null;
            object objDirecCasa = null;
            object objDirecVictima = null;
            object objDirecBloque = null;
            object objCacheModif = null;

            // en caso que no se logre lock de directorio bloque victima no deja pasar
            bool puedeContinuarDesdeBloqueVictima = true;

            Contexto contPrincipal = contextos.ElementAt(0);

            // Esto es de SC
            /// Si LL esta mal
            if (conditional && (contPrincipal.registro[32] != posMem || contPrincipal.loadLinkActivo == false))
            {
                contPrincipal.registro[regFuente] = 0;
                contPrincipal.loadLinkActivo = false;
                return -1;
            }
            try
            {
                // Intento bloquear mi caché
                Monitor.TryEnter(cacheDatos, ref bloqueoMiCache);
                if(bloqueoMiCache)
                {
                    // Esto es de SC
                    /// Invalido todos los otros LL
                    if (conditional)
                    {
                        foreach (var p in procesadores)
                        {
                            // para cada contexto en todos los procesadores que no sean el mio?????
                            foreach (var c in p.contextos)
                            {
                                if (p.id != this.id && c.loadLinkActivo)
                                {
                                    if (c.bloque_linked == contPrincipal.bloque_linked)
                                    {
                                        c.registro[32] = -1;
                                    }
                                }
                            }
                        }
                    }
                    #region bloqueoMiCache
                    objMiCache = cacheDatos;
                    var direccion = getPosicion(posMem);

                    if (bloqueEnMiCache(direccion.numeroBloque))
                    {
                        #region hitMiCache
                        // En este caso de da un HIT
                        int estadoMiBloque = cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO];
                        // Hay que revisar el estado del bloque
                        // Si está Modificado entonces modifiquelo de nuevo y ya
                        // Si está compartido hay que ir a invalidar a otros lados y luego modificar
                        switch (estadoMiBloque)
                        {
                            case ESTADO_MODIFICADO:
                                // Ya el estado esta modificado, solo cambio palabra
                                cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][direccion.numeroPalabra] = contPrincipal.registro[regFuente];
                                exito = 1;
                                break;
                            case ESTADO_COMPARTIDO:
                                // Busco el procesador que tiene el directorio casa del bloque
                                // lo intento bloquear
                                int numProc = getNumeroProcesador(direccion.numeroBloque);
                                Procesador procesadorDirecCasa = procesadores.ElementAt(numProc);
                                Monitor.TryEnter(procesadorDirecCasa.directorio, ref bloqueoDirecCasa);
                                if (bloqueoDirecCasa)
                                {
                                    // Si lo logro bloquear, esa función intentará invalidar, si lo logra
                                    // devuelve true y entonces puedo modificar el bloque
                                    objDirecCasa = procesadorDirecCasa.directorio;
                                    bool res = InvalidarCachesCompartidas(procesadorDirecCasa, direccion);
                                    if (res)
                                    {
                                        // Hago el cambio
                                        cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][direccion.numeroPalabra] = contPrincipal.registro[regFuente];
                                        cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_MODIFICADO;
                                        blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS] = direccion.numeroBloque;
                                        
                                        // Actualizo directorio                                   
                                        procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_MODIFICADO;
                                        procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][id + 1] = 1;
                                        exito = 2;
                                    }
                                    else
                                    {
                                        exito = -3;
                                    }
                                }
                                else
                                {
                                    exito = -2;
                                }
                                break;
                        }
                        #endregion
                    }
                    else
                    {
                        #region missMiCache
                        // En caso que haya MISS
                        // Primero veo el bloque víctima
                        int estadoBloqueVictima = cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO];
                        if (estadoBloqueVictima == ESTADO_COMPARTIDO || estadoBloqueVictima == ESTADO_MODIFICADO)
                        {
                            // Intento bloquear el directorio casa del bloque víctima
                            int numeroBloqueVictima = blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS];
                            Procesador procesadorBloqueVictima = procesadores.ElementAt(getNumeroProcesador(numeroBloqueVictima));
                            Monitor.TryEnter(procesadorBloqueVictima.directorio, ref bloqueoDirecVictima);
                            if (bloqueoDirecVictima)
                            {
                                #region bloqueoDirecVictima
                                objDirecVictima = procesadorBloqueVictima.directorio;
                                switch(estadoBloqueVictima)
                                {
                                    case ESTADO_COMPARTIDO:
                                        // Actualizo directorio
                                        procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][id + 1] = 0;

                                        // Reviso si tengo que ponerlo UNCACHED
                                        bool compartidoEnOtrasCaches = false;
                                        for (int i = 1; i < 4; i++)
                                        {
                                            if (procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][i + 1] == 1)
                                            {
                                                compartidoEnOtrasCaches = true;
                                            }
                                        }
                                        if (!compartidoEnOtrasCaches)
                                        {
                                            procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][0] = ESTADO_UNCACHED;
                                        }

                                        // Lo invalido en caché
                                        cacheDatos[numeroBloqueVictima % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_INVALIDO;
                                        blockMap[numeroBloqueVictima % CACHDAT_FILAS] = -1;
                                        break;
                                    case ESTADO_MODIFICADO:
                                        // manda a guardar el bloque a memoria 
                                        for (int i = 0; i < 4; i++)
                                        {
                                            procesadorBloqueVictima.memoriaPrincipal[numeroBloqueVictima % BLOQUES_COMP][i][0] = cacheDatos[numeroBloqueVictima % CACHDAT_FILAS][i];
                                        }

                                        // Actualizo directorio
                                        procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_UNCACHED;
                                        for (int i = 0; i < 3; i++)
                                        {
                                            procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][i + 1] = 0;
                                        }


                                        // Lo invalido en caché
                                        cacheDatos[numeroBloqueVictima % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_INVALIDO;
                                        blockMap[numeroBloqueVictima % CACHDAT_FILAS] = -1;
                                        break;
                                }
                                #endregion
                            }
                            else
                            {
                                puedeContinuarDesdeBloqueVictima = false;
                            }
                        }
                        if (puedeContinuarDesdeBloqueVictima)
                        {
                            // Si los locks salieron bien en la parte del bloque víctima, sigo
                            // Intento bloquear directorio casa del bloque que voy a cargar
                            int numProcBloque = getNumeroProcesador(direccion.numeroBloque);
                            Procesador procesadorDirecCasa = procesadores.ElementAt(numProcBloque);
                            Monitor.TryEnter(procesadorDirecCasa.directorio, ref bloqueoDirecBloque);

                            if(bloqueoDirecBloque)
                            {
                                #region bloqueoDirecBloque
                                // Tengo que fijarme en el estado del bloque:
                                objDirecBloque = procesadorDirecCasa.directorio;
                                int estadoBloque = procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][DIRECT_COL_ESTADO];
                                switch (estadoBloque)
                                {
                                    case ESTADO_UNCACHED:
                                        // Cargo de memoria a mi caché
                                        for(int i = 0; i < 4; i++)
                                        {
                                            cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][i] = procesadorDirecCasa.memoriaPrincipal[direccion.numeroBloque % BLOQUES_COMP][i][0];
                                        }
                                        blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS] = direccion.numeroBloque;

                                        // Actualizo directorio
                                        procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_MODIFICADO;
                                        procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][id + 1] = 1;

                                        // Modifico bloque y cambio estado
                                        cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][direccion.numeroPalabra] = contPrincipal.registro[regFuente];
                                        cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_MODIFICADO;

                                        exito = 3;
                                        break;
                                    case ESTADO_MODIFICADO:
                                        // Busco el procesador
                                        Procesador procesQueTieneModificado = null;
                                        for(int i = 0; i < procesadores.Count; i++)
                                        {
                                            if(i != id && procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][i + 1] == 1)
                                            {
                                                procesQueTieneModificado = procesadores.ElementAt(i);
                                            }
                                        }

                                        // Intento bloquear su caché
                                        Monitor.TryEnter(procesQueTieneModificado.cacheDatos, ref bloqueoCacheModif);
                                        if(bloqueoCacheModif && procesQueTieneModificado != null)
                                        {
                                            objCacheModif = procesQueTieneModificado.cacheDatos;
                                            
                                            for (int i = 0; i < 4; i++)
                                            {
                                                // copio a memoria
                                                procesadorDirecCasa.memoriaPrincipal[direccion.numeroBloque % BLOQUES_COMP][i][0] = procesQueTieneModificado.cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][i];
                                                // cargo a mi caché
                                                cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][i] = procesQueTieneModificado.cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][i];
                                            }
                                            // Cambio palabra
                                            cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][direccion.numeroPalabra] = contPrincipal.registro[regFuente];

                                            // Actualizo mi cache
                                            cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_MODIFICADO;
                                            blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS] = direccion.numeroBloque;

                                            // Actualizo caché otro procesador
                                            procesQueTieneModificado.cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_INVALIDO;
                                            procesQueTieneModificado.blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS] = -1;

                                            // Actualizo directorio
                                            procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][procesQueTieneModificado.id + 1] = 0;
                                            procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][id + 1] = 1;
                                            exito = 4;
                                        }
                                        else
                                        {
                                            exito = -6;
                                        }                     
                                        break;
                                    case ESTADO_COMPARTIDO:
                                        bool res = InvalidarCachesCompartidas(procesadorDirecCasa, direccion);
                                        if (res)
                                        {
                                            // Cambio palabra
                                            cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][direccion.numeroPalabra] = contPrincipal.registro[regFuente];

                                            // actualizo estado
                                            blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS] = direccion.numeroBloque;
                                            cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_MODIFICADO;

                                            // Actualizo directorio
                                            procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_MODIFICADO;
                                            procesadorDirecCasa.directorio[direccion.numeroBloque % DIRECT_FILAS][id + 1] = 1;
                                            exito = 5;
                                        }
                                        else
                                        {
                                            exito = -7;
                                        }
                                        break;
                                }
                                #endregion
                            }
                            else
                            {
                                exito = -5;
                            }
                        }
                        else
                        {
                            exito = -4;
                        }
                        #endregion
                    }
                    #endregion
                }
                else
                {
                    exito = -1;
                    // SC:
                    if (conditional)
                        contPrincipal.registro[regFuente] = 1;
                }
            }
            finally
            {
                if (bloqueoMiCache)
                    Monitor.Exit(objMiCache);
                if (bloqueoDirecCasa)
                    Monitor.Exit(objDirecCasa);
                if (bloqueoDirecVictima)
                    Monitor.Exit(objDirecVictima);
                if (bloqueoDirecBloque)
                    Monitor.Exit(objDirecBloque);
                if (bloqueoCacheModif)
                    Monitor.Exit(objCacheModif);
                if(exito < 1)
                    contPrincipal.pc -= 4;
            }
            return exito;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="regFuente2"></param>
        /// <param name="posMem"></param>
        /// <returns></returns>
        public int LoadWord(int regFuente2, int posMem)
        {
            int resultado = 0;

            // banderas de locks
            bool bloqueoMiCache = false;
            bool bloqueoDirecVictima = false;
            bool bloqueoDirecBloque = false;
            bool bloqueoCacheBloque = false;

            // punteros a los objetos que hay que liberar
            object objMiCache = null;
            object objDirecVictima = null;
            object objDirecBloque = null;
            object objCacheBloque = null;

            // en caso que no se logre lock de direc bloque victima no deja pasar
            bool puedeContinuarDesdeBloqueVictima = true;

            var direccion = getPosicion(posMem);
            Contexto contPrincipal = contextos.ElementAt(0);
            try
            {
                Monitor.TryEnter(cacheDatos, ref bloqueoMiCache);
                if (bloqueoMiCache)
                {
                    #region bloqueoMiCache
                    objMiCache = cacheDatos;
                    if (bloqueEnMiCache(direccion.numeroBloque))
                    {
                        // caso que hay HIT
                        // copie y vamonos
                        var fila = direccion.numeroBloque % CACHDAT_FILAS;
                        contPrincipal.registro[regFuente2] = cacheDatos[fila][direccion.numeroPalabra];
                        resultado = 1;
                    }
                    else
                    {
                        // caso que hay un MISS
                        // Hay que revisar el estado de bloque víctima 
                        int estadoBloqueVictima = cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO];
                        if (estadoBloqueVictima == ESTADO_COMPARTIDO || estadoBloqueVictima == ESTADO_MODIFICADO)
                        {
                            int numeroBloqueVictima = blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS];
                            Procesador procesadorBloqueVictima = procesadores.ElementAt(getNumeroProcesador(numeroBloqueVictima));
                            Monitor.TryEnter(procesadorBloqueVictima.directorio, ref bloqueoDirecVictima);
                            if (bloqueoDirecVictima)
                            {
                                #region bloqueoDirecVictima
                                // Logra bloquear el directorio víctima
                                objDirecVictima = procesadorBloqueVictima.directorio;
                                // Evalua el estado del bloque
                                switch(estadoBloqueVictima)
                                {
                                    case ESTADO_COMPARTIDO:
                                        // Actualiza el directorio
                                        procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][id + 1] = 0;

                                        // Ve a ver si tiene que poner UNCACHED
                                        bool compartidoEnOtrasCaches = false;
                                        for (int i = 0; i < 3; i++)
                                        {
                                            if (procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][i + 1] == 1)
                                            {
                                                compartidoEnOtrasCaches = true;
                                            }
                                        }
                                        if (!compartidoEnOtrasCaches)
                                        {
                                            procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_UNCACHED;
                                        }

                                        // Invalida la caché
                                        cacheDatos[numeroBloqueVictima % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_INVALIDO;
                                        blockMap[numeroBloqueVictima % CACHDAT_FILAS] = -1;
                                        break;
                                    case ESTADO_MODIFICADO:
                                        // Guardo en memoria
                                        for (int i = 0; i < 4; i++)
                                        {
                                            procesadorBloqueVictima.memoriaPrincipal[numeroBloqueVictima % BLOQUES_COMP][i][0] = cacheDatos[numeroBloqueVictima % CACHDAT_FILAS][i];
                                        }

                                        // Actualizo directorio
                                        procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_UNCACHED;
                                        for(int i = 0; i < 3; i++)
                                        {
                                            procesadorBloqueVictima.directorio[numeroBloqueVictima % DIRECT_FILAS][i + 1] = 0;
                                        }
                                        
                                        // Invalido en caché
                                        cacheDatos[numeroBloqueVictima % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_INVALIDO;
                                        blockMap[numeroBloqueVictima % CACHDAT_FILAS] = -1;
                                        break;
                                }
                                #endregion
                            }
                            else
                            {
                                puedeContinuarDesdeBloqueVictima = false;
                            }
                        }
                        if (puedeContinuarDesdeBloqueVictima)
                        {
                            // Significa que los locks del bloque victima se hicieron bien
                            int numProc = getNumeroProcesador(direccion.numeroBloque);
                            Procesador proceQueTieneElBloque = procesadores.ElementAt(numProc);

                            Monitor.TryEnter(proceQueTieneElBloque.directorio, ref bloqueoDirecBloque);
                            if (bloqueoDirecBloque)
                            {
                                // Se bloqueo el directorio casa del bloque
                                #region bloqueoDirecBloque
                                objDirecBloque = proceQueTieneElBloque.directorio;

                                /// Ciclos de retraso
                                if (proceQueTieneElBloque == this)
                                {
                                    estoyEnRetraso = true;
                                    ciclosEnRetraso += 2; // ciclos que gasta en consulta directorio local
                                }
                                else
                                {
                                    estoyEnRetraso = true;
                                    ciclosEnRetraso += 4; // ciclos que gasta en consulta directorio remoto
                                }

                                /// Veo el estado del bloque
                                int estadoBloque = proceQueTieneElBloque.directorio[direccion.numeroBloque % DIRECT_FILAS][DIRECT_COL_ESTADO];

                                /// Si alquien lo tiene modificado
                                if (estadoBloque == ESTADO_MODIFICADO)
                                {
                                    /// Busco el procesador
                                    Procesador procQueLoTieneM = null;
                                    for (int i = 0; i < 3; i++)
                                    {
                                        if (proceQueTieneElBloque.directorio[direccion.numeroBloque % DIRECT_FILAS][i + 1] == 1)
                                        {
                                            procQueLoTieneM = procesadores.ElementAt(i);
                                        }
                                    }

                                    Monitor.TryEnter(procQueLoTieneM.cacheDatos, ref bloqueoCacheBloque);
                                    if (bloqueoCacheBloque)
                                    {
                                        /// Logro bloquear la cache del procesador que tiene el bloque modificado
                                        objCacheBloque = procQueLoTieneM.cacheDatos;

                                        /// Guarda en memoria
                                        for (int i = 0; i < 4; i++)
                                        {
                                            proceQueTieneElBloque.memoriaPrincipal[direccion.numeroBloque % BLOQUES_COMP][i][0] = procQueLoTieneM.cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][i];
                                        }

                                        /// Actualiza estado de la caché
                                        procQueLoTieneM.cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][DIRECT_COL_ESTADO] = ESTADO_COMPARTIDO;

                                        /// Actualiza el directorio
                                        proceQueTieneElBloque.directorio[direccion.numeroBloque % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_COMPARTIDO;
                                        proceQueTieneElBloque.directorio[direccion.numeroBloque % DIRECT_FILAS][procQueLoTieneM.id + 1] = 1;
                                    }
                                }
                                /// Esta validación asegura que no hizo fail de try lock en el paso anterior
                                if (estadoBloque != ESTADO_MODIFICADO || (estadoBloque == ESTADO_MODIFICADO && bloqueoCacheBloque))
                                {
                                    /// Se trae el bloque de memoria a mi caché
                                    for (int i = 0; i < 4; i++)
                                    {
                                        cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][i] = proceQueTieneElBloque.memoriaPrincipal[direccion.numeroBloque % BLOQUES_COMP][i][0];
                                    }

                                    /// Actualizo mi caché
                                    cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][CACHDAT_COL_ESTADO] = ESTADO_COMPARTIDO;
                                    blockMapDatos[direccion.numeroBloque % CACHDAT_FILAS] = direccion.numeroBloque;

                                    /// Actualizo directorio
                                    proceQueTieneElBloque.directorio[direccion.numeroBloque % DIRECT_FILAS][DIRECT_COL_ESTADO] = ESTADO_COMPARTIDO;
                                    proceQueTieneElBloque.directorio[direccion.numeroBloque % DIRECT_FILAS][id + 1] = 1;

                                    /// Guardo en el registro
                                    contPrincipal.registro[regFuente2] = cacheDatos[direccion.numeroBloque % CACHDAT_FILAS][direccion.numeroPalabra];

                                    resultado = 2;
                                }
                                else
                                {
                                    resultado = -4;
                                }
                                #endregion
                            }
                            else
                            {
                                resultado = -3;
                            }
                        }
                        else
                        {
                            resultado = -2;
                        }
                    }
                    #endregion
                }
                else
                {
                    resultado = -1;
                }
            }
            finally
            {
                if (bloqueoMiCache)
                    Monitor.Exit(objMiCache);
                if (bloqueoDirecVictima)
                    Monitor.Exit(objDirecVictima);
                if (bloqueoDirecBloque)
                    Monitor.Exit(objDirecBloque);
                if (bloqueoCacheBloque)
                    Monitor.Exit(objCacheBloque);
                if (resultado < 1)
                    contPrincipal.pc -= 4;
            }
            return resultado;
        }

        public int loadLink(int regFuente2, int posMem)
        {
            var res = LoadWord(regFuente2, posMem);
            if (res > 0)
            {
                Contexto contPrincipal = contextos.ElementAt(0);
                var direccion = getPosicion(posMem);

                // Bandera de linked
                contPrincipal.loadLinkActivo = true;

                // Actualiza el valor de RL
                contPrincipal.registro[32] = posMem;

                // Guarda el numero de bloque
                contPrincipal.bloque_linked = direccion.numeroBloque;
            }
            return res;
        }


        public void Iniciar()
        {
            while (contextos.Count > 0)// && cicloActual < 215)
            {
                // Need to sync here
                
                sync.SignalAndWait();
                
                /// todo
                // console.WriteLine(String.Format("[Procesador #{0}] Hilillo #{1}, ciclo: {2}", id, contextos.ElementAt(0).id, cicloActual)); 
                if (!estoyEnRetraso)
                {

                    int pc = contextos.ElementAt(0).pc;
                    Direccion posicion = getPosicion(pc);
                    if (blockMap[posicion.numeroBloque % 4] != posicion.numeroBloque)
                    {
                        // Fallo de caché 
                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                //Console.WriteLine("i: "+i+" j: "+j);
                                cacheInstrucciones[posicion.numeroBloque % 4][j][i] = memoriaPrincipal[posicion.numeroBloque][j][i];
                                //Console.WriteLine("[{0}] Cache[{1}],[{2}],[{3}]=[{4}]", id, posicion.numeroBloque % 4, j, i, cacheInstrucciones[posicion.numeroBloque % 4][j][i]);
                            }
                        }
                        blockMap[posicion.numeroBloque % 4] = posicion.numeroBloque;

                        estoyEnRetraso = true;
                        ciclosEnRetraso = 16;
                        //Console.WriteLine("[{0}] Fallo de cache, ciclo: {1}", id, cicloActual);
                    }
                    else
                    {
                        bool res = manejoInstrucciones(cacheInstrucciones[posicion.numeroBloque % 4][posicion.numeroPalabra]);
                        if(res)
                        {
                            //Console.WriteLine("[{0}] Murio hilo {1}, ciclo: {2}", id, contextos.ElementAt(0).id, cicloActual);
                            contextos.ElementAt(0).cicloFinal = cicloActual;
                            contextosFinalizados.Add(contextos.ElementAt(0));
                            contextos.RemoveAt(0);
                        }
                        else
                        {
                            quantum--;
                            if (quantum == 0)
                            {
                                // Hacer cambio de contexto!
                                //Console.WriteLine("[{0}] Cambio contexto, ciclo: {1}", id, cicloActual); 
                                ShiftLeft(contextos, 1);
                                if (contextos.ElementAt(0).cicloInicial == -1)
                                    contextos.ElementAt(0).cicloInicial = cicloActual;
                            }
                        }   
                    }
                }
                else
                {
                    // si hay fallo de cache, el quantum no avanza
                    if (ciclosEnRetraso == 0)
                    {
                        estoyEnRetraso = false;
                    }
                    else
                    {
                        ciclosEnRetraso--;
                    }
                }
                cicloActual++;
            }
            
            sync.RemoveParticipant();
        }        
    }

}
