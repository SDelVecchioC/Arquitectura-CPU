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
        public int quantumGlobal;
        public int quantum;
        public List<Contexto> contextos, contextosFinalizados;

        //directorio 
        public int[][] directorio;
        public int[][] cacheDatos;
        public int[] blockMapDatos = new int[4];

        private Consola console;

        public List<Procesador> procesadores { get; set; }
        

        //referencia de otros procesadores 
        /*
        public Procesador proc1;
        public Procesador proc2; 
        **/
        public Procesador(int id, int maxCiclo, Barrier s, List<string> programas, Consola c, int recievedQuantum)// ref Procesador P1, ref Procesador P2)
        {
            console = c;
            this.sync = s;
            this.id = id;
            cicloActual = 1;
            this.maxCiclo = maxCiclo;
            falloCache = false;
            ciclosEnFallo = 0;
            // TODO recibir de usuario
            quantum = recievedQuantum;
            quantumGlobal = recievedQuantum;
            /*
            proc1 = P1;
            proc2 = P2;
            **/
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

            directorio = new int[8][];
            for (int j = 0; j < 8; j++)
            {
                directorio[j] = new int[4];
                for (int k = 0; k < 4; k++)
                {
                    if (k == 0)
                    {
                        directorio[j][k] = 0; //0 es U, 1 es C, 2 es M
                    }
                    else 
                    {
                        directorio[j][k] = -1;
                    }
                }
            }

            cacheDatos = new int[4][];
            for (int j = 0; j < 4; j++)
            {
                blockMapDatos[j] = -1;
                cacheDatos[j] = new int[6];
                for (int k = 0; k < 6; k++)
                {  //son 6 columnas, una para el estado, otra para la etiqueta del bloque y el resto para las 4 palabras
                    if (k == 0)
                    {
                        cacheDatos[j][k] = 0; //0 es U, 1 es C, 2 es M
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
                    memoriaPrincipal[i][j][0] = 1;
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

        public static void ShiftLeft<T>(List<T> lst, int shifts)
        {
            for (int i = 0; i < shifts; i++)
            {
                lst.Add(lst.ElementAt(0));
                lst.RemoveAt(0);
            }
        }


        public bool bloqueEnMiCache(Tuple<int, int> direccion)
        {
            return this.cacheDatos[direccion.Item1 % 4][1] == direccion.Item1;
        }

        public int getNumDirectorio(int posMem)
        {
            int numDirectorio = -1;
            numDirectorio = (int)posMem / 8;
            return numDirectorio; 
        }

        public void storeWord(int posMem, int regFuente)
        {

            bool bloqueoMiCache = false;

            var direccion = getPosicion(posMem);
            Contexto contPrincipal = contextos.ElementAt(0);
            try
            {
                Monitor.TryEnter(this.cacheDatos, ref bloqueoMiCache);
                if (bloqueoMiCache)
                {
                    #region bloqueoMiCache
                    // se pudo bloquear
                    if (bloqueEnMiCache(direccion))
                    {
                        #region HitEnMiCache
                        // hit en mi caché

                        int estadoMiBloque = this.cacheDatos[direccion.Item1 % 4][0];
                        //0 es I, 1 es C, 2 es M
                        // pregunta por el estado
                        switch (estadoMiBloque)
                        {
                            case 1:
                                #region Compartido
                                // el bloque esta C 
                                // pide directorio casa del bloque que esta C

                                int numProc = getNumeroProcesador(direccion.Item1);
                                bool bloqueoDirecCasa = false;
                                try
                                {
                                    Monitor.TryEnter(procesadores.ElementAt(numProc).directorio, 
                                        ref bloqueoDirecCasa);
                                    if (bloqueoDirecCasa)
                                    {
                                        // busco cuales otros lo tienen en C 
                                        // si hay, bloqueo caches e invalido
                                        // me devuelvo a lo mio y modifico
                                        // actualizo el directorio
                                    }
                                    else
                                    {
                                        //libero 
                                        bloqueoMiCache = false;
                                        Monitor.Exit(this.cacheDatos);
                                    }
                                }
                                finally
                                {
                                    if (bloqueoDirecCasa)
                                    {
                                        Monitor.Exit(procesadores.ElementAt(numProc).directorio);
                                    }
                                }
                                break;
                                #endregion
                            case 2:
                                //si el bloque esta M 
                                // escribe
                                memoriaPrincipal[direccion.Item1][direccion.Item2][0] = contPrincipal.registro[regFuente]; //no estoy segura que esa sea la pos de mem
                                break;
                        }
                        #endregion
                    }
                    else
                    {
                        #region MissEnMiCache
                        // miss en mi caché

                        int estadoBloqueVictima = cacheDatos[direccion.Item1 % 4][0];
                        #region estatusBloqueVictima
                        if (estadoBloqueVictima == 1 || estadoBloqueVictima == 2)
                        {
                            // pide directorio de bloque victima 
                            // lo bloquea 
                            int numeroBloqueVictima = cacheDatos[direccion.Item1 % 4][1];
                            int procesadorBloqueVictima = getNumeroProcesador(numeroBloqueVictima);
                            bool bloqueoDirecVictima = false;

                            try
                            {
                                Monitor.TryEnter(procesadores.ElementAt(procesadorBloqueVictima).directorio, ref bloqueoDirecVictima);
                                if (bloqueoDirecVictima)
                                {
                                    // bloqueo directorio victima

                                    if (estadoBloqueVictima == 1)
                                    {
                                        // actualiza el directorio poniendo cero
                                        // poner I cache propia
                                    }
                                    else if (estadoBloqueVictima == 2)
                                    {
                                        // manda a guardar el bloque   
                                        // actualizar directorio
                                        // poner I cache propia
                                    }
                                }
                                else
                                {
                                    // libero si no lo dan 
                                    bloqueoMiCache = false;
                                    Monitor.Exit(this.cacheDatos);
                                }
                            }
                            finally
                            {
                                if (bloqueoDirecVictima)
                                {
                                    Monitor.Exit(procesadores.ElementAt(numeroBloqueVictima).directorio);
                                }
                            }
                        }
                        #endregion
                        
                        int numProcBloque = getNumeroProcesador(direccion.Item1);
                        bool bloqueoDirecBloque = false;

                        try
                        {
                            Monitor.TryEnter(procesadores.ElementAt(numProcBloque).directorio, ref bloqueoDirecBloque);
                            if (bloqueoDirecBloque)
                            {
                                #region directorioDeBloqueDestino
                                // tengo directorio bloque que quiero leer
                                int estadoBloque = procesadores.ElementAt(numProcBloque).directorio[direccion.Item1 % 8][0];
                                // estados son U C M
                                switch(estadoBloque)
                                {
                                    case 0:
                                        // U
                                        // lo jala de memoria, lo guarda en mi cache y modifica el directorio
                                        // actualiza
                                        break;
                                    case 1:
                                        // C
                                        // fijarse en directorio, invalidar todo si alguien lo tiene
                                        // lo jala de memoria, lo guarda en mi cache y modifica el directorio
                                        break;
                                    case 2:
                                        // M
                                        // bloquea cache de donde esta
                                        // guarda en memoria, actualiza directorio
                                        // jala el bloque a mi cache y lo modifica
                                        // invalidar en la otra caché
                                        break;
                                }
                                #endregion
                            }
                            else
                            {
                                // libera
                            }
                        }
                        finally
                        {
                            Monitor.Exit(procesadores.ElementAt(numProcBloque).directorio);
                        }
                        #endregion
                    }
                }
                    #endregion
                else
                {
                    // Termina ciclo, vuelve a empezar 
                }
            }
            finally
            {
                if (bloqueoMiCache)
                {
                    Monitor.Exit(this.cacheDatos);
                }
            }

        }


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
                    res = String.Format("DADDI R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], regDest);
                    break;
                case 32:
                    /*
                    DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                    CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                    */
                    res = String.Format("DADD R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = String.Format("DSUB R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = String.Format("DMUL R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    res = String.Format("DDIV R{0},{1},{2}", regFuente1, contPrincipal.registro[regFuente2], contPrincipal.registro[regDest]);
                    break;
                case 4:
                    /*
                    BEQZ RX, ETIQ : Si RX = 0 salta 
                    CodOp: 4 RF1: Y RF2 O RD: 0 RD o IMM:n
                    */
                    res = String.Format("BEQZ R{0},{1}", contPrincipal.registro[regFuente1], regDest);
                    break;
                case 5:
                    /*
                     BEQNZ RX, ETIQ : Si RX != 0 salta 
                     CodOp: 5 RF1: x RF2 O RD: 0 RD o IMM:n
                     */
                    res = String.Format("BEQNZ R{0},{1}", contPrincipal.registro[regFuente1], regDest);
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
                    res = String.Format("JR RX{0}", contPrincipal.registro[regFuente1]);
                    break;
                case 63:
                    /*
                     fin
                     CodOp: 63 RF1: 0 RF2 O RD: 0 RD o IMM:0
                     */
                    res = String.Format("FIN");
                    break;
            }
            return res;
        }

        public bool manejoInstrucciones(int[] instruccion)
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
                    contPrincipal.registro[regFuente2] = contPrincipal.registro[regFuente1] + regDest;
                    break;
                case 32:
                    /*
                    DADD RX, RY, #n : Rx <-- (Ry) + (Rz)
                    CodOp: 32 RF1: Y RF2 O RD: x RD o IMM:Rz
                    */
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] + contPrincipal.registro[regFuente2];
                    break;
                case 34:
                    /*
                    DSUB RX, RY, #n : Rx <-- (Ry) - (Rz)
                    CodOp: 34 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] - contPrincipal.registro[regFuente2];
                    break;
                case 12:
                    /*
                    DMUL RX, RY, #n : Rx <-- (Ry) * (Rz)
                    CodOp: 12 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] * contPrincipal.registro[regFuente2];
                    break;
                case 14:
                    /*
                    DDIV RX, RY, #n : Rx <-- (Ry) / (Rz)
                    CodOp: 14 RF1: Y RF2 O RD: z RD o IMM:X
                    */
                    contPrincipal.registro[regDest] = contPrincipal.registro[regFuente1] / contPrincipal.registro[regFuente2];
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
                case 50:
                    /* *
                     * LL Rx, n(Ry)
                     * Rx <- M(n + (Ry))
                     * Rl <- n+(Ry)
                     * codOp: 50 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    break;
                case 51:
                    /* *
                     * SC RX, n(rY)
                     * IF (rl = N+(Ry)) => m(N+(RY)) = rX
                     * ELSE Rx =0
                     *  codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    break; 
                case 35:
                    /* *
                    * LW Rx, n(Ry)
                    * Rx <- M(n + (Ry))
                    * 
                    * codOp: 35 RF1: Y RF2 O RD: X RD O IMM: n
                    * */
                    break;
                case 43:
                    /* *
                     * SW RX, n(rY)
                     * m(N+(RY)) = rX
                     * codOp: 51 RF1: Y RF2 O RD: X RD O IMM: n
                     * */
                    break; 

            }
            return res;
        }

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
                        memoriaPrincipal[direccion.Item1][direccion.Item2][m] = numeros[m];
                        //Console.WriteLine("Memoria Principal[{0}][{1}][{2}]=[{3}])", direccion.Item1, direccion.Item2, m, memoriaPrincipal[direccion.Item1][direccion.Item2][m]);
                    }
                    direccionRam += 4;
                }
                idPrograma++;
            }
            contextos.ElementAt(0).cicloInicial = 1;
        }

        private int getNumeroProcesador(int bloque) 
        {
            return (int)(bloque / 8);
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

                if(quantum == quantumGlobal)
                {
                    console.WriteLine(String.Format("[Procesador #{0}] Hilillo #{1}, ciclo: {2}", id, contextos.ElementAt(0).id, cicloActual)); 
                }

                if (!falloCache)
                {

                    int pc = contextos.ElementAt(0).pc;
                    Tuple<int, int> posicion = getPosicion(pc);
                    if (blockMap[posicion.Item1 % 4] != posicion.Item1)
                    {
                        // Fallo de caché 
                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                //Console.WriteLine("i: "+i+" j: "+j);
                                cacheInstrucciones[posicion.Item1 % 4][j][i] = memoriaPrincipal[posicion.Item1][j][i];
                                //Console.WriteLine("[{0}] Cache[{1}],[{2}],[{3}]=[{4}]", id, posicion.Item1 % 4, j, i, cacheInstrucciones[posicion.Item1 % 4][j][i]);
                            }
                        }
                        blockMap[posicion.Item1 % 4] = posicion.Item1;

                        falloCache = true;
                        ciclosEnFallo = 16;
                        //Console.WriteLine("[{0}] Fallo de cache, ciclo: {1}", id, cicloActual);
                    }
                    else
                    {
                        /// @TODO Ejecutar mofo
                        //Console.WriteLine("[{0}] ciclo: {1}, [{2}]: {3}", id, cicloActual, contextos.ElementAt(0).id, getStringInstruccion(cacheInstrucciones[posicion.Item1 % 4][posicion.Item2]));
                        bool res = manejoInstrucciones(cacheInstrucciones[posicion.Item1 % 4][posicion.Item2]);
                        if(res)
                        {
                            //Console.WriteLine("[{0}] Murio hilo {1}, ciclo: {2}", id, contextos.ElementAt(0).id, cicloActual);
                            contextos.ElementAt(0).cicloFinal = cicloActual;
                            contextosFinalizados.Add(contextos.ElementAt(0));
                            contextos.RemoveAt(0);// @TODO contmanejorolar out of bounds
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
                                quantum = quantumGlobal;
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
            
            sync.RemoveParticipant();
        }

        public void loadWord(int regDest, int posMem)
        {
            bool bloqueoMiCache = false;
            Contexto contPrincipal = contextos.ElementAt(0);
            try
            {
                Monitor.TryEnter(this.cacheDatos, ref bloqueoMiCache);
                if (bloqueoMiCache)
                {
                    Tuple<int, int> posicion = getPosicion(posMem);
                    int cacheProc = getNumeroProcesador(posicion.Item1);
                    if (cacheProc == id)
                    {
                        // DEBERIA ESTAR EN MI CACHE
                        if (blockMapDatos[posicion.Item1 % 4] == posicion.Item1)
                        {
                            //HIT
                            //contPrincipal.registro[regDest] = memoriaPrincipal[posMem];
                        }
                        else
                        {
                            // MISS
                        }
                    }
                    else 
                    {
                        Monitor.Exit(this.cacheDatos);
                        // ESTA EN OTRO PROCESADOR
                    }
                }
                else
                {
                    // Barrera Barrera
                    //vuelve a empezar 

                }
            }
            finally
            {
                if (bloqueoMiCache)
                {
                    //Monitor.Exit(miCache);
                }
            }
        }




        public void setProcesadores(List<Procesador> p)
        {
            procesadores = p; 
        }

        
    }


  
}
