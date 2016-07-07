namespace Arquitectura_CPU
{
    internal class Contexto
    {
        public int Id, IdProc;
        public int CicloInicial, CicloFinal;
        public int[] Registro;
        public int Pc;
        public bool LoadLinkActivo; //se pierde en cambios de contexto
        public int BloqueLinked; // Indica el bloque linqueado
        public Contexto(int pc, int id, int idProc)
        {
            Registro = new int[33];
            for (var i = 0; i < 32; i++)
            {
                Registro[i] = 0;
            }

            Registro[32] = -1; // RL (se pierde en cambios de contexto)
            Pc = pc;
            Id = id;
            IdProc = idProc;
            CicloInicial = -1;
            BloqueLinked = -1; 
            LoadLinkActivo = false; 
        }
    }
}
