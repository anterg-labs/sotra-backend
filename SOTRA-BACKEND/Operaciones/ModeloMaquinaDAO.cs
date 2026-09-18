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

        public ModeloMaquina? seleccionarModeloMaquina(long id)
        {
            var modeloMaquina = contexto.ModeloMaquinas.FirstOrDefault(m => m.IdModeloMaquina == id);
            return modeloMaquina;
        }

        public ModeloMaquina insertarModeloMaquina(ModeloMaquina modeloMaquina)
        {
            contexto.ModeloMaquinas.Add(modeloMaquina);
            contexto.SaveChanges();
            return modeloMaquina;
        }

        public bool actualizarModeloMaquina(ModeloMaquina modeloMaquina)
        {
            var modeloExistente = contexto.ModeloMaquinas.FirstOrDefault(m => m.IdModeloMaquina == modeloMaquina.IdModeloMaquina);
            if (modeloExistente is null)
            {
                return false;
            }

            modeloExistente.Nombre = modeloMaquina.Nombre;
            modeloExistente.IdTipoMaquina = modeloMaquina.IdTipoMaquina;
            modeloExistente.Descripcion = modeloMaquina.Descripcion;
            modeloExistente.Activo = modeloMaquina.Activo;

            contexto.SaveChanges();
            return true;
        }

        public bool eliminarModeloMaquina(long id)
        {
            var modeloExistente = contexto.ModeloMaquinas.FirstOrDefault(m => m.IdModeloMaquina == id);
            if (modeloExistente is null)
            {
                return false;
            }

            contexto.ModeloMaquinas.Remove(modeloExistente);
            contexto.SaveChanges();
            return true;
        }

    }
}
