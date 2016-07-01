using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arquitectura_CPU
{
    class Contexto
    {
        public int id, idProc;
        public int cicloInicial, cicloFinal;
        public int[] registro;
        public int pc;
        public bool loadLinkActivo; //se pierde en cambios de contexto
        public int bloque_linked; // Indica el bloque linqueado
        public Contexto(int pc, int id, int idProc)
        {
            registro = new int[33];
            for (int i = 0; i < 32; i++)
            {
                registro[i] = 0;
            }

            registro[32] = -1; // RL (se pierde en cambios de contexto)
            this.pc = pc;
            this.id = id;
            this.idProc = idProc;
            this.cicloInicial = -1;
            bloque_linked = -1; 
            loadLinkActivo = false; 
        }
    }
}
