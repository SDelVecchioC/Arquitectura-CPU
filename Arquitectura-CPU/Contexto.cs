﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arquitectura_CPU
{
    class Contexto
    {
        public int id;
        public int[] registro;
        public int pc;

        public Contexto(int pc, int id)
        {
            registro = new int[32];
            for (int i = 0; i < 32; i++)
            {
                registro[i] = 0;
            }
            this.pc = pc;
            this.id = id;
        }
    }
}
