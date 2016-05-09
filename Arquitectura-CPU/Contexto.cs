using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arquitectura_CPU
{
    class Contexto
    {
        int[] registro;
        int pc;

        public Contexto(int pc)
        {
            registro = new int[32];
            this.pc = pc;
        }
    }
}
