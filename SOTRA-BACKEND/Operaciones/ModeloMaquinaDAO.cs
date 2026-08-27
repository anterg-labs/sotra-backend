using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;
using System;
using System.Collections.Generic;
using System.Text;

namespace SOTRA_BACKEND.Operaciones
{
    public class ModeloMaquinaDAO
    {
        public SotraContext contexto = new SotraContext();

        public List<ModeloMaquina> listarTodo()
        {
            var modelosMaquinas = contexto.ModeloMaquinas.ToList<ModeloMaquina>();
            return modelosMaquinas;
        }




    }
}
