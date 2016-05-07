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

        // referente a sincronizacion
        public Barrier sync;
        public int id, cicloActual, maxCiclo;

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
