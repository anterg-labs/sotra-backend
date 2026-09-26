using Api.Mapeadores;
using Api.DTOs.Usuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioDAO _usuarioDao;

    public UsuariosController(UsuarioDAO usuarioDao)
    {
        _usuarioDao = usuarioDao;
    }


    [HttpGet]
    [ProducesResponseType<List<UsuarioListadoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UsuarioListadoDto>>> ObtenerTodos(
        CancellationToken cancellationToken)
    {
        var respuesta = await _usuarioDao.ListarTodoAsync(
            UsuarioMapeador.Proyeccion, cancellationToken);
        return Ok(respuesta);
    }
}
// prueba
