using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arquitectura_CPU
{
    class HiloPrincipal
    {

        static void Main(string[] args)
        {
            var sync = new Barrier(participantCount: 3);

            // leer los archivos y repartirlos

            var cpu1 = new Procesador(1, 5, sync);
            var p1 = new Thread(cpu1.Iniciar);
            p1.Start();

            var p2 = new Thread(new Procesador(2, 5, sync).Iniciar);
            p2.Start();

            var p3 = new Thread(new Procesador(3, 5, sync).Iniciar);
            p3.Start();



            Console.ReadKey();
        }

    }
}
