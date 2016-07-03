using Arquitectura_CPU;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Interfaz
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public void escribirRegistro(string s, int i)
        {
            switch (i)
            {
                case 0:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => registros1.Text = s));
                    break;
                case 1:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => registros2.Text = s));
                    break;
                case 2:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => registros3.Text = s));
                    break;
            }
        }
        public void escribirCache(string s, int i)
        {
            switch (i)
            {
                case 0:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => cache1.Text = s));
                    break;
                case 1:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => cache2.Text = s));
                    break;
                case 2:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => cache3.Text = s));
                    break;
            }
        }
        public void escribirMemo(string s, int i)
        {
            switch (i)
            {
                case 0:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => memo1.Text = s));
                    break;
                case 1:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => memo2.Text = s));
                    break;
                case 2:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => memo3.Text = s));
                    break;
            }
        }
        public void escribirDirectorios(string s, int i)
        {
            switch (i)
            {
                case 0:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => direct1.Text = s));
                    break;
                case 1:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => direct2.Text = s));
                    break;
                case 2:
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => direct3.Text = s));
                    break;
            }
        }

        public void escribirInstruccion(string s)
        {
            Application.Current.Dispatcher.BeginInvoke(
                       DispatcherPriority.Background,
                       new Action(() => Instrucciones.Text += s));
        }

        Consola consola;
        Barrier sync, barreraMaestra, barreraProcesadores;
        List<Procesador> procesadores;
        List<Thread> hilos;
        List<string> programas;
        int cantProcesadores;

        public MainWindow()
        {
            InitializeComponent();

            int quantum = int.Parse(txtQuantum.Text);
            cantProcesadores = 3;
            consola = new Consola(this);
            sync = new Barrier(participantCount: cantProcesadores);
            barreraProcesadores = new Barrier(participantCount: cantProcesadores + 1);
            barreraMaestra = new Barrier(participantCount: 2);


            programas = new List<string>();
            procesadores = new List<Procesador>();
            hilos = new List<Thread>();

            // Lee los archivos y los reparte
            foreach (string file in Directory.EnumerateFiles("./programas", "*.txt"))
            {
                string contents = File.ReadAllText(file);
                programas.Add(contents);
            }

            List<List<string>> programasPorCpu = SplitList(programas, cantProcesadores);

            // creacion de procesadores
            for (int i = 0; i < cantProcesadores; i++)
            {
                var cpu = new Procesador(i, sync, barreraProcesadores, programasPorCpu.ElementAt(i), quantum);
                procesadores.Add(cpu);
            }

            // creacion de hilos
            for (int i = 0; i < cantProcesadores; i++)
            {
                var proc = procesadores.ElementAt(i);
                proc.setProcesadores(procesadores);
                var hiloCpu = new Thread(proc.Iniciar);
                hilos.Add(hiloCpu);
                hiloCpu.Start();
            }

            var clase = new HiloPrincipal(consola, procesadores, barreraProcesadores, barreraMaestra);
            var hiloPrincipal = new Thread(clase.Iniciar);
            hiloPrincipal.Start();

        }

        private List<List<T>> SplitList<T>(List<T> locations, int nSize)
        {
            List<List<T>> res = new List<List<T>>();
            for (int i = 0; i < nSize; i++)
            {
                var l = new List<T>();
                res.Add(l);
            }
            for (int i = 0; i < locations.Count; i++)
            {
                res.ElementAt(i % nSize).Add(locations.ElementAt(i));
            }
            return res;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            barreraMaestra.SignalAndWait();
        }
    }

    class HiloPrincipal{
        Consola consola;
        List<Procesador> procesadores;
        Barrier barreraMaestra;
        Barrier barreraProcesadores;

        public HiloPrincipal(Consola consola, List<Procesador> procesadores, Barrier barreraProcesadores, Barrier barreraMaestra)
        {
            this.consola = consola;
            this.procesadores = procesadores;
            this.barreraProcesadores = barreraProcesadores;
            this.barreraMaestra = barreraMaestra;
        }

        public void Iniciar()
        {
            foreach (var proc in procesadores)
            {
                consola.imprimirProcesador(proc);
            }
            while (procesadores.ElementAt(0).contextos.Count > 0 || procesadores.ElementAt(1).contextos.Count > 0 || procesadores.ElementAt(2).contextos.Count > 0)
            {
                string instruccion = "";
                barreraMaestra.SignalAndWait();
                foreach (var proc in procesadores)
                {
                    if (proc.contextos.Count > 0)
                    {
                        instruccion += proc.getInst();
                        proc.EjecutarInstruccion();
                    }
                }
                consola.escribirInstruccion(instruccion);
                barreraProcesadores.SignalAndWait();
            }
        }
    }
}
