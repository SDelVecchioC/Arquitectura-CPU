using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Arquitectura_CPU
{
    class Procesador
    {
        #region constantes
        // constantes: para salud mental

        public const int CachdatFilas = 4;
        public const int CachdatColumnas = 5;
        public const int CachdatColEstado = 4;

        public const int EstadoInvalido = 0;
        public const int EstadoCompartido = 1;
        public const int EstadoModificado = 2;

        public const int DirectFilas = 8;
        public const int DirectColumnas = 4;
        public const int DirectColEstado = 0;

        public const int EstadoUncached = 0;
        public const int BloquesComp = 8;
        #endregion

        /// <summary>
        /// Struct para representar una direccion de memoria
        /// </summary>
        public struct Direccion
        {
            public int NumeroBloque;
            public int NumeroPalabra;
        }

        // estructuras de datos del procesador
        public int[][][] CacheInstrucciones;
        public int[][][] MemoriaPrincipal;
        public int[] BlockMap = new int[4];

        private bool _estoyEnRetraso;
        private int _ciclosEnRetraso;

        // referente a sincronizacion
        public Barrier Sync;
        public int Id, CicloActual;
        public int Quantum;
        public List<Contexto> Contextos, ContextosFinalizados;

        // directorio 
        // 8 columnas -> cantidad de bloques memo compart
        // 4 columnas -> 0 estado, 3 cada procesador
        public int[][] Directorio;

        // cacheDatos[4][5]
        // 4 filas -> cantidad de bloques de la cache
        // 5 columnas -> 0 para ESTADO, 4 de DATOS
        public int[][] CacheDatos;
        public int[] BlockMapDatos;

        private readonly Consola _console;

        public List<Procesador> Procesadores { get; set; }

        public Procesador(int id, Barrier s, List<string> programas, Consola c, int recievedQuantum)
        {
            _console = c;
            Sync = s;
            Id = id;
            CicloActual = 1;
            _estoyEnRetraso = false;
            _ciclosEnRetraso = 0;

            Procesadores = new List<Procesador>();

            Quantum = recievedQuantum;

            CacheInstrucciones = new int[4][][];
            for (int i = 0; i < 4; i++)
            {
                BlockMap[i] = -1;
                CacheInstrucciones[i] = new int[4][];
                for (int j = 0; j < 4; j++)
                {
                    CacheInstrucciones[i][j] = new int[4];
                    for (int k = 0; k < 4; k++)
                    {
                        CacheInstrucciones[i][j][k] = 0;
                    }
                }
            }

            Directorio = new int[DirectFilas][];
            for (int j = 0; j < DirectFilas; j++)
            {
                Directorio[j] = new int[DirectColumnas];
                for (int k = 0; k < DirectColumnas; k++)
                {
                    Directorio[j][k] = 0;
                }
            }

            CacheDatos = new int[CachdatFilas][];
            BlockMapDatos = new int[CachdatFilas];

            for (int j = 0; j < CachdatFilas; j++)
            {
                BlockMapDatos[j] = -1;
                CacheDatos[j] = new int[CachdatColumnas];
                for (int k = 0; k < CachdatColumnas; k++)
                {  
                    if (k == CachdatColEstado)
                    {
                        CacheDatos[j][k] = EstadoInvalido;
                    }
                    else
                    {
                        CacheDatos[j][k] = -1;
                    }
                }
            }

            MemoriaPrincipal = new int[24][][];
            //parte compartida
            for (int i = 0; i < 8; i++)
            {
                MemoriaPrincipal[i] = new int[4][];
                for (int j = 0; j < 4; j++)
                {
                    MemoriaPrincipal[i][j] = new int[4];
                    for(int k = 0; k < 4; k++)
                    {
                        MemoriaPrincipal[i][j][k] = 1;
                    }
                    
                }
            }
            //parte no compartida 
            for (int i = 8; i < 24; i++)
            {
                MemoriaPrincipal[i] = new int[4][];
                for (int j = 0; j < 4; j++)
                {
                    MemoriaPrincipal[i][j] = new int[4];
                    for (int k = 0; k < 4; k++)
                    {
                        MemoriaPrincipal[i][j][k] = 0;
                    }
                }
            }

            Contextos = new List<Contexto>();
            ContextosFinalizados = new List<Contexto>();
            ManejoArchivo(programas);

        }

        /// <summary>
        /// Recibe y almacena las referencias de los procesadores
        /// </summary>
        public void SetProcesadores(ref Procesador p1, ref Procesador p2, ref Procesador p3)
        {
            Procesadores.Add(p1);
            Procesadores.Add(p2);
            Procesadores.Add(p3);
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
        public void ManejoArchivo(List<string> programas)
        {
            int direccionRam = 128;
            int idPrograma = 1;
            foreach (var p in programas)
            {
                // para cada programa
                // cada linea es una instruccion de 4 numeros
                string[] instrucciones = p.Split('\n');

                Contexto contexto = new Contexto(direccionRam, idPrograma, Id);
                Contextos.Add(contexto);

                foreach (var i in instrucciones)
                {
                    // para cada instruccion separo los 4 numeros
                    string[] instruccion = i.Split(' ');
                    int[] numeros = Array.ConvertAll(instruccion, int.Parse);

                    for (int m = 0; m < 4; m++)
                    {
                        var direccion = GetPosicion(direccionRam);
                        MemoriaPrincipal[direccion.NumeroBloque][direccion.NumeroPalabra][m] = numeros[m];
                    }
                    direccionRam += 4;
                }
                idPrograma++;
            }
            Contextos.ElementAt(0).cicloInicial = 1;
        }
        
        /// <summary>
        /// Genera la instrucción bonita basada en los códigos de operación
        /// </summary>
        /// <param name="instruccion"></param>
        /// <returns></returns>
        public string GetStringInstruccion(int[] instruccion)
        {
            int codigoInstruccion = instruccion[0],
                regFuente1 = instruccion[1],
                regFuente2 = instruccion[2],
                regDest = instruccion[3];
            string res = "";

            switch (codigoInstruccion)
            {
                case 8:
                    /*
                    DADDI RX, RY, #n : Rx <-- (Ry) + n
                    CodOp: 8 RF1: Y RF2 O RD: x RD O IMM:n
                    */
                    res = $"DADDI R{regFuente2},R{regFuente1},{regDest}";
                    break;
                case 32:
                    /*
                    DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                    CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                    */
                    res = $"DADD R{regDest},R{regFuente1},R{regFuente2}";
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = $"DSUB R{regDest},R{regFuente1},R{regFuente2}";
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = $"DMUL R{regDest},R{regFuente1},R{regFuente2}";
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = $"DDIV R{regDest},R{regFuente1},R{regFuente2}";
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
                    res = $"BEQZ R{regFuente1},{regDest}";
                    break;
                case 5:
                    /*
                     BEQNZ RX, ETIQ : Si RX != 0 salta 
                     CodOp: 5 RF1: x RF2 O RD: 0 RD o IMM:n
                     */
                    res = $"BNEQZ R{regFuente1},{regDest}";
                    break;
                case 3:
                    /*
                    JAL n, R31=PC, PC = PC+n
                    CodOp: 3 RF1: 0 RF2 O RD: 0 RD o IMM:n
                    */
                    res = $"JAL {regDest}";
                    break;
                case 2:
                    /*
                    JR RX: PC=RX
                    CodOp: 2 RF1: X RF2 O RD: 0 RD o IMM:0
                    */
                    res = $"JR R{regFuente1}";
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    res = "FIN";
                    break;
                case 35:
                    /* *
                    * LW Rx, n(Ry)
                    * Rx <- M(n + (Ry))
                    * 
                    * codOp: 35 RF1: Y RF2 O RD: X RD O IMM: n
                    * */
                    res = $"LW R{regFuente2} {regDest}(R{regFuente1})";
                    break;
                case 50:
                    /* *
                     * LL Rx, n(Ry)
                     * Rx <- M(n + (Ry))
                     * Rl <- n+(Ry)
                     * codOp: 50 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    res = $"LL R{regFuente2} {regDest}(R{regFuente1})";
                    break;
                case 51:
                    /* *
                     * SC RX, n(rY)
                     * IF (rl = N+(Ry)) => m(N+(RY)) = rX
                     * ELSE Rx =0
                     *  codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    res = $"SC R{regFuente2} {regDest}(R{regFuente1})";
                    break;
                case 43:
                    /* *
                     * SW RX, n(rY)
                     * m(N+(RY)) = rX
                     * codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    res = $"SW R{regFuente2} {regDest}(R{regFuente1})";
                    break;
            }
            return res;
        }

        /// <summary>
        /// Procesa las instrucciones
        /// </summary>
        /// <param name="instruccion"></param>
        /// <returns></returns>
        public bool ManejoInstrucciones(int[] instruccion)
        {
            bool res = false;
            int codigoInstruccion = instruccion[0],
                regFuente1 = instruccion[1],
                regFuente2 = instruccion[2],
                regDest = instruccion[3],
                posMem;

            Contexto contPrincipal = Contextos.ElementAt(0);

            string output = "";
            int pc = Contextos.ElementAt(0).pc;
            Direccion posicion = GetPosicion(pc);

            switch (codigoInstruccion)
            {
                case 8:
                    /*
                    DADDI RX, RY, #n : Rx <-- (Ry) + n
                    CodOp: 8 RF1: Y RF2 O RD: x RD O IMM:n
                    */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    contPrincipal.registro[regFuente2] = contPrincipal.registro[regFuente1] + regDest;
                    break;
                case 32:
                    /*
                    DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                    CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                    */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] + contPrincipal.registro[regFuente2];
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] - contPrincipal.registro[regFuente2];
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] * contPrincipal.registro[regFuente2];
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] / contPrincipal.registro[regFuente2];
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
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
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
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
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    contPrincipal.registro[31] = contPrincipal.pc;
                    contPrincipal.pc += regDest;
                    break;
                case 2:
                    /*
                    JR RX: PC=RX
                    CodOp: 2 RF1: X RF2 O RD: 0 RD o IMM:0
                    */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    contPrincipal.pc = contPrincipal.registro[regFuente1];
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    res = true;
                    break;
                case 50:
                    /* *
                     * LL Rx, n(Ry)
                     * Rx <- M(n + (Ry))
                     * Rl <- n+(Ry)
                     * codOp: 50 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    posMem = contPrincipal.registro[regFuente1] + regDest;
                    var ll = LoadLink(regFuente2, posMem);
                    output += " Pos: " + posMem + " Res: " + ll.ToString();
                    break;
                case 51:
                    /* *
                     * SC RX, n(rY)
                     * IF (rl = N+(Ry)) => m(N+(RY)) = rX
                     * ELSE Rx =0
                     *  codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
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
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
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
                    output =
                        $"[{Id}] ciclo: {CicloActual}, [{Contextos.ElementAt(0).id}]: {GetStringInstruccion(CacheInstrucciones[posicion.NumeroBloque%4][posicion.NumeroPalabra])}";
                    posMem = contPrincipal.registro[regFuente1] + regDest;
                    int storeRes = StoreWord(posMem, regFuente2);
                    output += " Pos: " + posMem + " Res: " + storeRes.ToString();
                    break;
            }

            _console.WriteLine(output);
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
        private static int GetNumeroProcesador(int numeroBloque)
        {
            return numeroBloque / BloquesComp;
        }
        
        /// <summary>
        /// Calcula la direccion para un bloque de memoria
        /// </summary>
        /// <param name="direccion"></param>
        /// <returns></returns>
        private static Direccion GetPosicion(int direccion)
        {
            int bloque = direccion / 16;
            int palabra = (direccion % 16) / 4;
            Direccion d;
            d.NumeroBloque = bloque;
            d.NumeroPalabra = palabra;
            return d;
        }

        /// <summary>
        /// Determina si un bloque está en la caché de datos del procesador
        /// </summary>
        /// <returns>True si el bloque está en caché</returns>
        public bool BloqueEnMiCache(int numeroBloque)
        {
            return BlockMapDatos[numeroBloque % CachdatFilas] == numeroBloque;
        }

        /// <summary>
        /// Invalida entrada de una caché con bloque Compartida, actualiza entrada directorio
        /// </summary>
        /// <param name="procesadorDirecCasa"></param>
        /// <param name="procAInvalidar"></param>
        /// <param name="direccion"></param>
        private static void InvalidarCacheProcesador(Procesador procesadorDirecCasa, Procesador procAInvalidar, Direccion direccion)
        {
            // Invalida caché
            procAInvalidar.CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado] = EstadoInvalido;
            procAInvalidar.BlockMapDatos[direccion.NumeroBloque % CachdatFilas] = -1;

            // Actualiza el directorio
            procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][procAInvalidar.Id + 1] = 0;
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

            // Busca los procesadores
            List<Procesador> procesadoresTienenComp = new List<Procesador>();
            for (int i = 0; i < 3; i++)
            {
                if (i != Id && procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][i + 1] == 1)
                    procesadoresTienenComp.Add(Procesadores.ElementAt(i));
            }

            for (int i = 0; i < procesadoresTienenComp.Count && exito; i++)
            {
                Procesador proc = procesadoresTienenComp.ElementAt(i);
                try
                {
                    // Logra bloquear caché
                    bloqueoCache = false;
                    Monitor.TryEnter(proc.CacheDatos, ref bloqueoCache);
                    if (!bloqueoCache)
                        exito = false;
                    else
                        InvalidarCacheProcesador(procesadorDirecCasa, proc, direccion);
                }
                finally
                {
                    if (bloqueoCache)
                        Monitor.Exit(proc.CacheDatos);
                }
            }
            return exito;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="posMem"></param>
        /// <param name="regFuente"></param>
        /// <param name="conditional"></param>
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
            var bloqueoCacheModif = false;

            // Punteros que hay que liberar
            object objMiCache = null;
            object objDirecCasa = null;
            object objDirecVictima = null;
            object objDirecBloque = null;
            object objCacheModif = null;

            // en caso que no se logre lock de directorio bloque victima no deja pasar
            bool puedeContinuarDesdeBloqueVictima = true;

            Contexto contPrincipal = Contextos.ElementAt(0);

            // Esto es de SC
            // Si LL esta mal
            if (conditional && (contPrincipal.registro[32] != posMem || contPrincipal.loadLinkActivo == false))
            {
                contPrincipal.registro[regFuente] = 0;
                contPrincipal.loadLinkActivo = false;
                return -1;
            }
            try
            {
                // Intento bloquear mi caché
                Monitor.TryEnter(CacheDatos, ref bloqueoMiCache);
                if(bloqueoMiCache)
                {
                    // Esto es de SC
                    // Invalido todos los otros LL
                    if (conditional)
                    {
                        foreach (var p in Procesadores)
                        {
                            // para cada contexto en todos los procesadores que no sean el mio?????
                            foreach (var c in p.Contextos)
                            {
                                if (p.Id != Id && c.loadLinkActivo)
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
                    objMiCache = CacheDatos;
                    var direccion = GetPosicion(posMem);

                    if (BloqueEnMiCache(direccion.NumeroBloque))
                    {
                        #region hitMiCache
                        // En este caso de da un HIT
                        int estadoMiBloque = CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado];
                        // Hay que revisar el estado del bloque
                        // Si está Modificado entonces modifiquelo de nuevo y ya
                        // Si está compartido hay que ir a invalidar a otros lados y luego modificar
                        switch (estadoMiBloque)
                        {
                            case EstadoModificado:
                                // Ya el estado esta modificado, solo cambio palabra
                                CacheDatos[direccion.NumeroBloque % CachdatFilas][direccion.NumeroPalabra] = contPrincipal.registro[regFuente];
                                exito = 1;
                                break;
                            case EstadoCompartido:
                                // Busco el procesador que tiene el directorio casa del bloque
                                // lo intento bloquear
                                int numProc = GetNumeroProcesador(direccion.NumeroBloque);
                                Procesador procesadorDirecCasa = Procesadores.ElementAt(numProc);
                                Monitor.TryEnter(procesadorDirecCasa.Directorio, ref bloqueoDirecCasa);
                                if (bloqueoDirecCasa)
                                {
                                    // Si lo logro bloquear, esa función intentará invalidar, si lo logra
                                    // devuelve true y entonces puedo modificar el bloque
                                    objDirecCasa = procesadorDirecCasa.Directorio;
                                    bool res = InvalidarCachesCompartidas(procesadorDirecCasa, direccion);
                                    if (res)
                                    {
                                        // Actualizo directorio
                                        procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][DirectColEstado] = EstadoModificado;
                                        procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][Id + 1] = 1;

                                        // Hago el cambio
                                        CacheDatos[direccion.NumeroBloque % CachdatFilas][direccion.NumeroPalabra] = contPrincipal.registro[regFuente];
                                        CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado] = EstadoModificado;

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
                        int estadoBloqueVictima = CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado];
                        if (estadoBloqueVictima == EstadoCompartido || estadoBloqueVictima == EstadoModificado)
                        {
                            // Intento bloquear el directorio casa del bloque víctima
                            int numeroBloqueVictima = BlockMapDatos[direccion.NumeroBloque % CachdatFilas];
                            Procesador procesadorBloqueVictima = Procesadores.ElementAt(GetNumeroProcesador(numeroBloqueVictima));
                            Monitor.TryEnter(procesadorBloqueVictima.Directorio, ref bloqueoDirecVictima);
                            if (bloqueoDirecVictima)
                            {
                                #region bloqueoDirecVictima
                                objDirecVictima = procesadorBloqueVictima.Directorio;
                                switch(estadoBloqueVictima)
                                {
                                    case EstadoCompartido:
                                        // Actualizo directorio
                                        procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][Id + 1] = 0;

                                        // Reviso si tengo que ponerlo UNCACHED
                                        bool compartidoEnOtrasCaches = false;
                                        for (int i = 1; i < 4; i++)
                                        {
                                            if (procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][i + 1] == 1)
                                            {
                                                compartidoEnOtrasCaches = true;
                                            }
                                        }
                                        if (!compartidoEnOtrasCaches)
                                        {
                                            procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][0] = EstadoUncached;
                                        }

                                        // Lo invalido en caché
                                        CacheDatos[numeroBloqueVictima % CachdatFilas][CachdatColEstado] = EstadoInvalido;
                                        BlockMap[numeroBloqueVictima % CachdatFilas] = -1;
                                        break;
                                    case EstadoModificado:
                                        // manda a guardar el bloque a memoria 
                                        for (int i = 0; i < 4; i++)
                                        {
                                            procesadorBloqueVictima.MemoriaPrincipal[numeroBloqueVictima % BloquesComp][i][0] = CacheDatos[numeroBloqueVictima % CachdatFilas][i];
                                        }

                                        // Actualizo directorio
                                        procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][Id + 1] = 0;
                                        procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][DirectColEstado] = EstadoUncached;

                                        // Lo invalido en caché
                                        CacheDatos[numeroBloqueVictima % CachdatFilas][CachdatColEstado] = EstadoInvalido;
                                        BlockMap[numeroBloqueVictima % CachdatFilas] = -1;
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
                            int numProcBloque = GetNumeroProcesador(direccion.NumeroBloque);
                            Procesador procesadorDirecCasa = Procesadores.ElementAt(numProcBloque);
                            Monitor.TryEnter(procesadorDirecCasa.Directorio, ref bloqueoDirecBloque);

                            if(bloqueoDirecBloque)
                            {
                                #region bloqueoDirecBloque
                                // Tengo que fijarme en el estado del bloque:
                                // UNCACHED: cargo de memoria, actualizo direct, actualizo estado caché
                                // MODIFICADO: busco el procesador que lo tiene modificado, intentlo bloquear su cache
                                // si sí, bajo a memoria, copio en mi caché, actualizo cachés y directorio 
                                // COMPARTIDO: intento invalidar otras cachés y si si, cambio la mía y actualizo
                                objDirecBloque = procesadorDirecCasa.Directorio;
                                int estadoBloque = procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][DirectColEstado];
                                switch (estadoBloque)
                                {
                                    case EstadoUncached:
                                        // Cargo de memoria a mi caché
                                        for(int i = 0; i < 4; i++)
                                        {
                                            CacheDatos[direccion.NumeroBloque % CachdatFilas][i] = procesadorDirecCasa.MemoriaPrincipal[direccion.NumeroBloque % BloquesComp][i][0];
                                        }
                                        BlockMapDatos[direccion.NumeroBloque % CachdatFilas] = direccion.NumeroBloque;

                                        // Actualizo directorio
                                        procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][DirectColEstado] = EstadoModificado;
                                        procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][Id + 1] = 1;

                                        // Modifico bloque y cambio estado
                                        CacheDatos[direccion.NumeroBloque % CachdatFilas][direccion.NumeroPalabra] = contPrincipal.registro[regFuente];
                                        CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado] = EstadoModificado;

                                        exito = 3;
                                        break;
                                    case EstadoModificado:
                                        // Busco el procesador
                                        Procesador procesQueTieneModificado = null;
                                        for(int i = 0; i < 3; i++)
                                        {
                                            if(i != Id && procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][i + 1] == 1)
                                            {
                                                procesQueTieneModificado = Procesadores.ElementAt(i);
                                            }
                                        }

                                        // Intento bloquear su caché
                                        Debug.Assert(procesQueTieneModificado != null, "procesQueTieneModificado != null");
                                        Monitor.TryEnter(procesQueTieneModificado.CacheDatos, ref bloqueoCacheModif);
                                        if(bloqueoCacheModif)
                                        {
                                            objCacheModif = procesQueTieneModificado.CacheDatos;
                                            
                                            for (int i = 0; i < 4; i++)
                                            {
                                                // copio a memoria
                                                procesadorDirecCasa.MemoriaPrincipal[direccion.NumeroBloque % BloquesComp][i][0] = procesQueTieneModificado.CacheDatos[direccion.NumeroBloque % CachdatFilas][i];
                                                // cargo a mi caché
                                                CacheDatos[direccion.NumeroBloque % CachdatFilas][i] = procesQueTieneModificado.CacheDatos[direccion.NumeroBloque % CachdatFilas][i];
                                            }
                                            // Cambio palabra, actualizo estado
                                            CacheDatos[direccion.NumeroBloque % CachdatFilas][direccion.NumeroPalabra] = contPrincipal.registro[regFuente];
                                            CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado] = EstadoModificado;
                                            BlockMapDatos[direccion.NumeroBloque % CachdatFilas] = direccion.NumeroBloque;

                                            // Actualizo caché otro procesador
                                            procesQueTieneModificado.CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado] = EstadoInvalido;
                                            procesQueTieneModificado.BlockMapDatos[direccion.NumeroBloque % CachdatFilas] = -1;

                                            // Actualizo directorio
                                            procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][procesQueTieneModificado.Id + 1] = 0;
                                            procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][Id + 1] = 1;
                                            exito = 4;
                                        }
                                        else
                                        {
                                            exito = -6;
                                        }                     
                                        break;
                                    case EstadoCompartido:
                                        bool res = InvalidarCachesCompartidas(procesadorDirecCasa, direccion);
                                        if (res)
                                        {
                                            // Cambio palabra, actualizo estado
                                            CacheDatos[direccion.NumeroBloque % CachdatFilas][direccion.NumeroPalabra] = contPrincipal.registro[regFuente];
                                            CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado] = EstadoModificado;

                                            // Actualizo directorio
                                            procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][DirectColEstado] = EstadoModificado;
                                            procesadorDirecCasa.Directorio[direccion.NumeroBloque % DirectFilas][Id + 1] = 1;
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

                    // SC:
                    if (conditional)
                    {
                        contPrincipal.registro[regFuente] = 1;
                        contPrincipal.loadLinkActivo = false;
                    }
                }
                else
                {
                    exito = -1;
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

            var direccion = GetPosicion(posMem);
            Contexto contPrincipal = Contextos.ElementAt(0);
            try
            {
                Monitor.TryEnter(CacheDatos, ref bloqueoMiCache);
                if (bloqueoMiCache)
                {
                    #region bloqueoMiCache
                    objMiCache = CacheDatos;
                    if (BloqueEnMiCache(direccion.NumeroBloque))
                    {
                        // caso que hay HIT
                        // copie y vamonos
                        var fila = direccion.NumeroBloque % CachdatFilas;
                        contPrincipal.registro[regFuente2] = CacheDatos[fila][direccion.NumeroPalabra];
                        resultado = 1;
                    }
                    else
                    {
                        // caso que hay un MISS
                        // Hay que revisar el estado de bloque víctima 
                        var estadoBloqueVictima = CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado];
                        if (estadoBloqueVictima == EstadoCompartido || estadoBloqueVictima == EstadoModificado)
                        {
                            int numeroBloqueVictima = BlockMapDatos[direccion.NumeroBloque % CachdatFilas];
                            Procesador procesadorBloqueVictima = Procesadores.ElementAt(GetNumeroProcesador(numeroBloqueVictima));
                            Monitor.TryEnter(procesadorBloqueVictima.Directorio, ref bloqueoDirecVictima);
                            if (bloqueoDirecVictima)
                            {
                                #region bloqueoDirecVictima
                                // Logra bloquear el directorio víctima
                                objDirecVictima = procesadorBloqueVictima.Directorio;
                                // Ciclos de retraso
                                if (procesadorBloqueVictima == this)
                                {
                                    _estoyEnRetraso = true;
                                    _ciclosEnRetraso += 2; // ciclos que gasta en consulta directorio local
                                }
                                else
                                {
                                    _estoyEnRetraso = true;
                                    _ciclosEnRetraso += 4; // ciclos que gasta en consulta directorio remoto
                                }

                                // Evalua el estado del bloque
                                // Si está compartido, lo invalida y actualiza cachés y directorios
                                // Si está modificado, hace eso mismo pero adémás guarda en memoria
                                switch(estadoBloqueVictima)
                                {
                                    case EstadoCompartido:
                                        // Actualiza el directorio
                                        procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][Id + 1] = 0;

                                        // Ve a ver si tiene que poner UNCACHED
                                        bool compartidoEnOtrasCaches = false;
                                        for (int i = 0; i < 3; i++)
                                        {
                                            if (procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][i + 1] == 1)
                                            {
                                                compartidoEnOtrasCaches = true;
                                            }
                                        }
                                        if (!compartidoEnOtrasCaches)
                                        {
                                            procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][DirectColEstado] = EstadoUncached;
                                        }

                                        // Invalida la caché
                                        CacheDatos[numeroBloqueVictima % CachdatFilas][CachdatColEstado] = EstadoInvalido;
                                        BlockMap[numeroBloqueVictima % CachdatFilas] = -1;
                                        break;
                                    case EstadoModificado:
                                        // Guardo en memoria
                                        for (int i = 0; i < 4; i++)
                                        {
                                            procesadorBloqueVictima.MemoriaPrincipal[numeroBloqueVictima % BloquesComp][i][0] = CacheDatos[numeroBloqueVictima % CachdatFilas][i];
                                        }

                                        // Ciclos de retraso
                                        _estoyEnRetraso = true;
                                        _ciclosEnRetraso += 16;

                                        // Actualizo directorio
                                        procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][DirectColEstado] = EstadoUncached;
                                        procesadorBloqueVictima.Directorio[numeroBloqueVictima % DirectFilas][Id + 1] = 0;

                                        // Invalido en caché
                                        CacheDatos[numeroBloqueVictima % CachdatFilas][CachdatColEstado] = EstadoInvalido;
                                        BlockMap[numeroBloqueVictima % CachdatFilas] = -1;
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
                            int numProc = GetNumeroProcesador(direccion.NumeroBloque);
                            Procesador proceQueTieneElBloque = Procesadores.ElementAt(numProc);

                            Monitor.TryEnter(proceQueTieneElBloque.Directorio, ref bloqueoDirecBloque);
                            if (bloqueoDirecBloque)
                            {
                                // Se bloqueo el directorio casa del bloque
                                #region bloqueoDirecBloque
                                objDirecBloque = proceQueTieneElBloque.Directorio;

                                // Ciclos de retraso
                                if (proceQueTieneElBloque == this)
                                {
                                    _estoyEnRetraso = true;
                                    _ciclosEnRetraso += 2; // ciclos que gasta en consulta directorio local
                                }
                                else
                                {
                                    _estoyEnRetraso = true;
                                    _ciclosEnRetraso += 4; // ciclos que gasta en consulta directorio remoto
                                }

                                // Veo el estado del bloque
                                var estadoBloque = proceQueTieneElBloque.Directorio[direccion.NumeroBloque % DirectFilas][DirectColEstado];

                                // Si alquien lo tiene modificado
                                if (estadoBloque == EstadoModificado)
                                {
                                    // Busco el procesador
                                    Procesador procQueLoTieneM = null;
                                    for (int i = 0; i < 3; i++)
                                    {
                                        if (proceQueTieneElBloque.Directorio[direccion.NumeroBloque % DirectFilas][i + 1] == 1)
                                        {
                                            procQueLoTieneM = Procesadores.ElementAt(i);
                                        }
                                    }

                                    Debug.Assert(procQueLoTieneM != null, "procQueLoTieneM != null");
                                    Monitor.TryEnter(procQueLoTieneM.CacheDatos, ref bloqueoCacheBloque);
                                    if (bloqueoCacheBloque)
                                    {
                                        // Logro bloquear la cache del procesador que tiene el bloque modificado
                                        objCacheBloque = procQueLoTieneM.CacheDatos;

                                        // Guarda en memoria
                                        for (var i = 0; i < 4; i++)
                                        {
                                            proceQueTieneElBloque.MemoriaPrincipal[direccion.NumeroBloque % BloquesComp][i][0] = procQueLoTieneM.CacheDatos[direccion.NumeroBloque % CachdatFilas][i];
                                        }

                                        // Actualiza estado de la caché
                                        procQueLoTieneM.CacheDatos[direccion.NumeroBloque % CachdatFilas][DirectColEstado] = EstadoCompartido;

                                        // Actualiza el directorio
                                        proceQueTieneElBloque.Directorio[direccion.NumeroBloque % DirectFilas][DirectColEstado] = EstadoCompartido;
                                        proceQueTieneElBloque.Directorio[direccion.NumeroBloque % DirectFilas][procQueLoTieneM.Id + 1] = 1;
                                    }
                                }
                                // Esta validación asegura que no hizo fail de try lock en el paso anterior
                                if (estadoBloque != EstadoModificado || (estadoBloque == EstadoModificado && bloqueoCacheBloque))
                                {
                                    // Se trae el bloque de memoria a mi caché
                                    for (var i = 0; i < 4; i++)
                                    {
                                        CacheDatos[direccion.NumeroBloque % CachdatFilas][i] = proceQueTieneElBloque.MemoriaPrincipal[direccion.NumeroBloque % BloquesComp][i][0];
                                    }

                                    // Actualizo mi caché
                                    CacheDatos[direccion.NumeroBloque % CachdatFilas][CachdatColEstado] = EstadoCompartido;
                                    BlockMapDatos[direccion.NumeroBloque % CachdatFilas] = direccion.NumeroBloque;

                                    // Actualizo directorio
                                    proceQueTieneElBloque.Directorio[direccion.NumeroBloque % DirectFilas][DirectColEstado] = EstadoCompartido;
                                    proceQueTieneElBloque.Directorio[direccion.NumeroBloque % DirectFilas][Id + 1] = 1;

                                    // Guardo en el registro
                                    contPrincipal.registro[regFuente2] = CacheDatos[direccion.NumeroBloque % CachdatFilas][direccion.NumeroPalabra];

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

        public int LoadLink(int regFuente2, int posMem)
        {
            var res = LoadWord(regFuente2, posMem);
            if (res > 0)
            {
                Contexto contPrincipal = Contextos.ElementAt(0);
                var direccion = GetPosicion(posMem);

                // Bandera de linked
                contPrincipal.loadLinkActivo = true;

                // Actualiza el valor de RL
                contPrincipal.registro[32] = posMem;

                // Guarda el numero de bloque
                contPrincipal.bloque_linked = direccion.NumeroBloque;
            }
            return res;
        }


        public void Iniciar()
        {
            while (Contextos.Count > 0 && CicloActual < 215)
            {
                // Need to sync here
                
                Sync.SignalAndWait();
                
                // todo
                // console.WriteLine(String.Format("[Procesador #{0}] Hilillo #{1}, ciclo: {2}", id, contextos.ElementAt(0).id, cicloActual)); 
                if (!_estoyEnRetraso)
                {

                    int pc = Contextos.ElementAt(0).pc;
                    Direccion posicion = GetPosicion(pc);
                    if (BlockMap[posicion.NumeroBloque % 4] != posicion.NumeroBloque)
                    {
                        // Fallo de caché 
                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                //Console.WriteLine("i: "+i+" j: "+j);
                                CacheInstrucciones[posicion.NumeroBloque % 4][j][i] = MemoriaPrincipal[posicion.NumeroBloque][j][i];
                                //Console.WriteLine("[{0}] Cache[{1}],[{2}],[{3}]=[{4}]", id, posicion.numeroBloque % 4, j, i, cacheInstrucciones[posicion.numeroBloque % 4][j][i]);
                            }
                        }
                        BlockMap[posicion.NumeroBloque % 4] = posicion.NumeroBloque;

                        _estoyEnRetraso = true;
                        _ciclosEnRetraso = 16;
                        //Console.WriteLine("[{0}] Fallo de cache, ciclo: {1}", id, cicloActual);
                    }
                    else
                    {
                        bool res = ManejoInstrucciones(CacheInstrucciones[posicion.NumeroBloque % 4][posicion.NumeroPalabra]);
                        if(res)
                        {
                            //Console.WriteLine("[{0}] Murio hilo {1}, ciclo: {2}", id, contextos.ElementAt(0).id, cicloActual);
                            Contextos.ElementAt(0).cicloFinal = CicloActual;
                            ContextosFinalizados.Add(Contextos.ElementAt(0));
                            Contextos.RemoveAt(0);
                        }
                        else
                        {
                            Quantum--;
                            if (Quantum == 0)
                            {
                                // Hacer cambio de contexto!
                                //Console.WriteLine("[{0}] Cambio contexto, ciclo: {1}", id, cicloActual); 
                                ShiftLeft(Contextos, 1);
                                if (Contextos.ElementAt(0).cicloInicial == -1)
                                    Contextos.ElementAt(0).cicloInicial = CicloActual;
                            }
                        }   
                    }
                }
                else
                {
                    // si hay fallo de cache, el quantum no avanza
                    if (_ciclosEnRetraso == 0)
                    {
                        _estoyEnRetraso = false;
                    }
                    else
                    {
                        _ciclosEnRetraso--;
                    }
                }
                CicloActual++;
            }
            
            Sync.RemoveParticipant();
        }        
    }

}
