using Microsoft.Data.SqlClient;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.Data
{
    public class ProductoRepository
    {
        private const string SinAcentos = "COLLATE Latin1_General_CI_AI";

        /// <summary>
        /// Inventario paginado con una fila por variante. Primero las variantes sin stock,
        /// después las que están bajo el mínimo y al final el resto (alfabético).
        /// </summary>
        public List<FilaInventario> ObtenerInventarioPaginado(string busqueda, int? categoriaId,
                                                               int pagina, int porPagina,
                                                               out int totalRegistros,
                                                               bool soloInactivos = false,
                                                               bool soloConVariantes = false)
        {
            var lista = new List<FilaInventario>();

            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var where = soloInactivos ? "WHERE p.activo = 0" : "WHERE p.activo = 1";
            if (soloConVariantes)
                where += " AND v.id IS NOT NULL";
            if (!string.IsNullOrWhiteSpace(busqueda))
                // CI_AI: la búsqueda ignora mayúsculas y acentos ("cano" encuentra "Caño")
                where += $" AND (p.nombre {SinAcentos} LIKE @busqueda OR p.codigo_interno {SinAcentos} LIKE @busqueda OR c.nombre {SinAcentos} LIKE @busqueda OR p.marca {SinAcentos} LIKE @busqueda OR v.medida {SinAcentos} LIKE @busqueda)";
            if (categoriaId.HasValue)
                where += " AND p.categoria_id = @catId";

            const string from = @"
                FROM productos p
                LEFT JOIN categorias c ON c.id = p.categoria_id
                LEFT JOIN variantes_producto v ON v.producto_id = p.id";

            var cmdCount = new SqlCommand($"SELECT COUNT(*) {from} {where}", conn);
            AgregarParametrosBusqueda(cmdCount, busqueda, categoriaId);
            totalRegistros = (int)cmdCount.ExecuteScalar();

            var cmdData = new SqlCommand($@"
                SELECT p.id, p.codigo_interno, p.nombre, p.marca, c.nombre AS nombre_categoria, p.activo,
                       v.id AS var_id, v.medida, v.precio_unitario, v.stock_actual, v.stock_minimo
                {from}
                {where}
                ORDER BY
                    CASE WHEN v.id IS NULL              THEN 1
                         WHEN v.stock_actual <= 0       THEN 0
                         WHEN v.stock_actual < v.stock_minimo THEN 1
                         ELSE 2 END,
                    p.nombre, v.medida, v.id
                OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY", conn);

            AgregarParametrosBusqueda(cmdData, busqueda, categoriaId);
            cmdData.Parameters.AddWithValue("@offset", (pagina - 1) * porPagina);
            cmdData.Parameters.AddWithValue("@porPagina", porPagina);

            using var r = cmdData.ExecuteReader();
            while (r.Read())
            {
                var sinVariante = r.IsDBNull(6);
                lista.Add(new FilaInventario
                {
                    ProductoId = r.GetInt32(0),
                    CodigoInterno = r.GetString(1),
                    Nombre = r.GetString(2),
                    Marca = r.IsDBNull(3) ? null : r.GetString(3),
                    NombreCategoria = r.IsDBNull(4) ? null : r.GetString(4),
                    Activo = r.GetBoolean(5),
                    VarianteId = sinVariante ? null : r.GetInt32(6),
                    Medida = r.IsDBNull(7) ? null : r.GetString(7),
                    PrecioUnitario = sinVariante ? 0 : r.GetDecimal(8),
                    StockActual = sinVariante ? 0 : r.GetInt32(9),
                    StockMinimo = sinVariante ? 0 : r.GetInt32(10),
                });
            }

            return lista;
        }

        public Producto? ObtenerPorId(int id)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT p.id, p.codigo_interno, p.nombre, p.marca,
                       p.categoria_id, c.nombre AS nombre_categoria,
                       p.proveedor, p.activo,
                       v.id AS var_id, v.medida, v.calidad,
                       v.precio_unitario, v.stock_actual, v.stock_minimo
                FROM productos p
                LEFT JOIN categorias c ON c.id = p.categoria_id
                LEFT JOIN variantes_producto v ON v.producto_id = p.id
                WHERE p.id = @id", conn);

            cmd.Parameters.AddWithValue("@id", id);

            Producto? prod = null;
            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                prod ??= new Producto
                {
                    Id = r.GetInt32(r.GetOrdinal("id")),
                    CodigoInterno = r.GetString(r.GetOrdinal("codigo_interno")),
                    Nombre = r.GetString(r.GetOrdinal("nombre")),
                    Marca = r.IsDBNull(r.GetOrdinal("marca")) ? null : r.GetString(r.GetOrdinal("marca")),
                    CategoriaId = r.IsDBNull(r.GetOrdinal("categoria_id")) ? 0 : r.GetInt32(r.GetOrdinal("categoria_id")),
                    NombreCategoria = r.IsDBNull(r.GetOrdinal("nombre_categoria")) ? null : r.GetString(r.GetOrdinal("nombre_categoria")),
                    Proveedor = r.IsDBNull(r.GetOrdinal("proveedor")) ? null : r.GetString(r.GetOrdinal("proveedor")),
                    Activo = r.GetBoolean(r.GetOrdinal("activo")),
                };

                if (!r.IsDBNull(r.GetOrdinal("var_id")))
                {
                    prod.Variantes.Add(new VarianteProducto
                    {
                        Id = r.GetInt32(r.GetOrdinal("var_id")),
                        ProductoId = prod.Id,
                        Medida = r.IsDBNull(r.GetOrdinal("medida")) ? null : r.GetString(r.GetOrdinal("medida")),
                        Calidad = r.IsDBNull(r.GetOrdinal("calidad")) ? null : r.GetString(r.GetOrdinal("calidad")),
                        PrecioUnitario = r.GetDecimal(r.GetOrdinal("precio_unitario")),
                        StockActual = r.GetInt32(r.GetOrdinal("stock_actual")),
                        StockMinimo = r.GetInt32(r.GetOrdinal("stock_minimo")),
                    });
                }
            }

            return prod;
        }

        public (int total, int criticos, decimal valorTotal) ObtenerMetricas()
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT
                    COUNT(DISTINCT p.id)                                          AS total,
                    SUM(CASE WHEN v.stock_actual < v.stock_minimo THEN 1 ELSE 0 END) AS criticos,
                    SUM(v.stock_actual * v.precio_unitario)                       AS valor
                FROM productos p
                LEFT JOIN variantes_producto v ON v.producto_id = p.id
                WHERE p.activo = 1", conn);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return (0, 0, 0);

            return (
                r.IsDBNull(0) ? 0 : r.GetInt32(0),
                r.IsDBNull(1) ? 0 : r.GetInt32(1),
                r.IsDBNull(2) ? 0 : r.GetDecimal(2)
            );
        }

        public int ObtenerVentasHoy()
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM ventas
                WHERE CAST(fecha_hora AS DATE) = CAST(GETDATE() AS DATE)", conn);

            return (int)cmd.ExecuteScalar();
        }

        public List<Categoria> ObtenerCategorias()
        {
            var lista = new List<Categoria>();
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT c.id, c.nombre,
                       (SELECT COUNT(*) FROM productos p WHERE p.categoria_id = c.id) AS cant
                FROM categorias c
                ORDER BY c.nombre", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new Categoria { Id = r.GetInt32(0), Nombre = r.GetString(1), CantidadProductos = r.GetInt32(2) });

            return lista;
        }

        public int InsertarCategoria(string nombre)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(
                "INSERT INTO categorias (nombre) VALUES (@nom); SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@nom", nombre);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>Renombra solo si la categoría no tiene productos (sus códigos dependen del nombre).</summary>
        public bool RenombrarCategoria(int id, string nombre)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                UPDATE categorias SET nombre = @nom
                WHERE id = @id
                  AND NOT EXISTS (SELECT 1 FROM productos WHERE categoria_id = @id)", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@nom", nombre);
            return cmd.ExecuteNonQuery() == 1;
        }

        /// <summary>Elimina solo si la categoría no tiene productos (ni activos ni desactivados).</summary>
        public bool EliminarCategoria(int id)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                DELETE FROM categorias
                WHERE id = @id
                  AND NOT EXISTS (SELECT 1 FROM productos WHERE categoria_id = @id)", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() == 1;
        }

        /// <summary>Último código usado con ese prefijo (ej. "CON-014"), o null si no hay ninguno.</summary>
        public string? ObtenerUltimoCodigo(string prefijo)
        {
            var ultimo = ObtenerUltimoNumero(prefijo);
            return ultimo > 0 ? $"{prefijo}-{ultimo:D3}" : null;
        }

        private int ObtenerUltimoNumero(string prefijo)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(
                    TRY_CAST(SUBSTRING(codigo_interno, LEN(@pre) + 2, 20) AS INT)
                ), 0)
                FROM productos
                WHERE codigo_interno LIKE @pre + '-%'", conn);
            cmd.Parameters.AddWithValue("@pre", prefijo);
            return (int)cmd.ExecuteScalar();
        }

        private static void AgregarParametrosBusqueda(SqlCommand cmd,
                                                        string? busqueda,
                                                        int? categoriaId)
        {
            if (!string.IsNullOrWhiteSpace(busqueda))
                cmd.Parameters.AddWithValue("@busqueda", $"%{busqueda}%");
            if (categoriaId.HasValue)
                cmd.Parameters.AddWithValue("@catId", categoriaId.Value);
        }

        public int InsertarProducto(Producto p)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmdP = new SqlCommand(@"
                    INSERT INTO productos
                        (codigo_interno, nombre, marca, categoria_id, proveedor)
                    VALUES
                        (@cod, @nom, @marca, @catId, @prov);
                    SELECT SCOPE_IDENTITY();", conn, tx);

                cmdP.Parameters.AddWithValue("@cod", p.CodigoInterno);
                cmdP.Parameters.AddWithValue("@nom", p.Nombre);
                cmdP.Parameters.AddWithValue("@marca", (object?)p.Marca ?? DBNull.Value);
                cmdP.Parameters.AddWithValue("@catId", p.CategoriaId > 0 ? (object)p.CategoriaId : DBNull.Value);
                cmdP.Parameters.AddWithValue("@prov", (object?)p.Proveedor ?? DBNull.Value);

                var nuevoId = Convert.ToInt32(cmdP.ExecuteScalar());

                foreach (var v in p.Variantes)
                    InsertarVarianteConTransaccion(conn, tx, nuevoId, v);

                tx.Commit();
                return nuevoId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void ActualizarProducto(Producto p)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmdP = new SqlCommand(@"
                    UPDATE productos SET
                        codigo_interno          = @cod,
                        nombre                  = @nom,
                        marca                   = @marca,
                        categoria_id            = @catId,
                        proveedor               = @prov
                    WHERE id = @id", conn, tx);

                cmdP.Parameters.AddWithValue("@id", p.Id);
                cmdP.Parameters.AddWithValue("@cod", p.CodigoInterno);
                cmdP.Parameters.AddWithValue("@nom", p.Nombre);
                cmdP.Parameters.AddWithValue("@marca", (object?)p.Marca ?? DBNull.Value);
                cmdP.Parameters.AddWithValue("@catId", p.CategoriaId > 0 ? (object)p.CategoriaId : DBNull.Value);
                cmdP.Parameters.AddWithValue("@prov", (object?)p.Proveedor ?? DBNull.Value);
                cmdP.ExecuteNonQuery();

                var idsEnBD = ObtenerIdsVariantes(conn, tx, p.Id);
                var idsEnForm = p.Variantes.Where(v => v.Id > 0).Select(v => v.Id).ToHashSet();

                foreach (var idViejo in idsEnBD.Except(idsEnForm))
                    EliminarVarianteConTransaccion(conn, tx, idViejo);

                foreach (var v in p.Variantes)
                {
                    if (v.Id > 0)
                        ActualizarVarianteConTransaccion(conn, tx, v);
                    else
                        InsertarVarianteConTransaccion(conn, tx, p.Id, v);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static HashSet<int> ObtenerIdsVariantes(SqlConnection conn, SqlTransaction tx, int productoId)
        {
            var cmd = new SqlCommand(
                "SELECT id FROM variantes_producto WHERE producto_id = @pid",
                conn, tx);
            cmd.Parameters.AddWithValue("@pid", productoId);

            var ids = new HashSet<int>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt32(0));
            return ids;
        }

        private static void InsertarVarianteConTransaccion(SqlConnection conn, SqlTransaction tx, int productoId, VarianteProducto v)
        {
            var cmd = new SqlCommand(@"
                INSERT INTO variantes_producto
                    (producto_id, medida, calidad, precio_unitario, stock_actual, stock_minimo)
                VALUES
                    (@pid, @medida, @calidad, @precio, @stock, @smin)",
                conn, tx);

            cmd.Parameters.AddWithValue("@pid", productoId);
            cmd.Parameters.AddWithValue("@medida", (object?)v.Medida ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@calidad", (object?)v.Calidad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@precio", v.PrecioUnitario);
            cmd.Parameters.AddWithValue("@stock", v.StockActual);
            cmd.Parameters.AddWithValue("@smin", v.StockMinimo);
            cmd.ExecuteNonQuery();
        }

        private static void ActualizarVarianteConTransaccion(SqlConnection conn, SqlTransaction tx, VarianteProducto v)
        {
            var cmd = new SqlCommand(@"
                UPDATE variantes_producto SET
                    medida          = @medida,
                    calidad         = @calidad,
                    precio_unitario = @precio,
                    stock_actual    = @stock,
                    stock_minimo    = @smin
                WHERE id = @id",
                conn, tx);

            cmd.Parameters.AddWithValue("@id", v.Id);
            cmd.Parameters.AddWithValue("@medida", (object?)v.Medida ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@calidad", (object?)v.Calidad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@precio", v.PrecioUnitario);
            cmd.Parameters.AddWithValue("@stock", v.StockActual);
            cmd.Parameters.AddWithValue("@smin", v.StockMinimo);
            cmd.ExecuteNonQuery();
        }

        private static void EliminarVarianteConTransaccion(SqlConnection conn, SqlTransaction tx, int varianteId)
        {
            var cmd = new SqlCommand(
                "DELETE FROM variantes_producto WHERE id = @id",
                conn, tx);
            cmd.Parameters.AddWithValue("@id", varianteId);
            cmd.ExecuteNonQuery();
        }

        public List<Producto> ObtenerTodosConVariantes()
        {
            var productos = new Dictionary<int, Producto>();

            const string sql = @"
                SELECT
                    p.id, p.codigo_interno, p.nombre, p.marca, p.proveedor,
                    c.id   AS cat_id,   c.nombre AS cat_nombre,
                    v.id   AS var_id,   v.medida, v.calidad,
                    v.precio_unitario,  v.stock_actual, v.stock_minimo
                FROM productos p
                LEFT JOIN categorias          c ON c.id = p.categoria_id
                LEFT JOIN variantes_producto  v ON v.producto_id = p.id
                WHERE p.activo = 1
                ORDER BY p.nombre, v.medida";

            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            using var rd = cmd.ExecuteReader();

            while (rd.Read())
            {
                int pid = rd.GetInt32(0);
                if (!productos.TryGetValue(pid, out var prod))
                {
                    prod = new Producto
                    {
                        Id = pid,
                        CodigoInterno = rd.GetString(1),
                        Nombre = rd.GetString(2),
                        Marca = rd.IsDBNull(3) ? null : rd.GetString(3),
                        Proveedor = rd.IsDBNull(4) ? null : rd.GetString(4),
                        CategoriaId = rd.IsDBNull(5) ? 0 : rd.GetInt32(5),
                        NombreCategoria = rd.IsDBNull(6) ? "" : rd.GetString(6),
                    };
                    productos[pid] = prod;
                }

                if (!rd.IsDBNull(7))
                {
                    prod.Variantes.Add(new VarianteProducto
                    {
                        Id = rd.GetInt32(7),
                        ProductoId = pid,
                        Medida = rd.IsDBNull(8) ? null : rd.GetString(8),
                        Calidad = rd.IsDBNull(9) ? null : rd.GetString(9),
                        PrecioUnitario = rd.GetDecimal(10),
                        StockActual = rd.IsDBNull(11) ? 0 : rd.GetInt32(11),
                        StockMinimo = rd.IsDBNull(12) ? 5 : rd.GetInt32(12),
                    });
                }
            }

            return productos.Values.ToList();
        }
        public Producto? ObtenerProductoPorVariante(int varianteId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
        SELECT p.id, p.codigo_interno, p.nombre, p.marca,
               p.categoria_id, c.nombre AS nombre_categoria,
               p.proveedor,
               v.id AS var_id, v.medida, v.calidad,
               v.precio_unitario, v.stock_actual, v.stock_minimo
        FROM productos p
        LEFT JOIN categorias c ON c.id = p.categoria_id
        LEFT JOIN variantes_producto v ON v.producto_id = p.id
        WHERE v.id = @varianteId", conn);

            cmd.Parameters.AddWithValue("@varianteId", varianteId);

            Producto? prod = null;

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                prod ??= new Producto
                {
                    Id = r.GetInt32(r.GetOrdinal("id")),
                    CodigoInterno = r.GetString(r.GetOrdinal("codigo_interno")),
                    Nombre = r.GetString(r.GetOrdinal("nombre")),
                    Marca = r.IsDBNull(r.GetOrdinal("marca")) ? null : r.GetString(r.GetOrdinal("marca")),
                    CategoriaId = r.IsDBNull(r.GetOrdinal("categoria_id")) ? 0 : r.GetInt32(r.GetOrdinal("categoria_id")),
                    NombreCategoria = r.IsDBNull(r.GetOrdinal("nombre_categoria")) ? null : r.GetString(r.GetOrdinal("nombre_categoria")),
                    Proveedor = r.IsDBNull(r.GetOrdinal("proveedor")) ? null : r.GetString(r.GetOrdinal("proveedor")),
                };

                prod.Variantes.Add(new VarianteProducto
                {
                    Id = r.GetInt32(r.GetOrdinal("var_id")),
                    ProductoId = prod.Id,
                    Medida = r.IsDBNull(r.GetOrdinal("medida")) ? null : r.GetString(r.GetOrdinal("medida")),
                    Calidad = r.IsDBNull(r.GetOrdinal("calidad")) ? null : r.GetString(r.GetOrdinal("calidad")),
                    PrecioUnitario = r.GetDecimal(r.GetOrdinal("precio_unitario")),
                    StockActual = r.GetInt32(r.GetOrdinal("stock_actual")),
                    StockMinimo = r.GetInt32(r.GetOrdinal("stock_minimo")),
                });
            }

            return prod;
        }

        public bool TieneVentasAsociadas(int productoId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM detalle_ventas dv
                    INNER JOIN variantes_producto v ON v.id = dv.variante_id
                    WHERE v.producto_id = @id
                ) THEN 1 ELSE 0 END", conn);
            cmd.Parameters.AddWithValue("@id", productoId);

            return (int)cmd.ExecuteScalar() == 1;
        }

        public void DesactivarProducto(int productoId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand("UPDATE productos SET activo = 0 WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", productoId);
            cmd.ExecuteNonQuery();
        }

        public void ReactivarProducto(int productoId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand("UPDATE productos SET activo = 1 WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", productoId);
            cmd.ExecuteNonQuery();
        }

        public (int id, string nombre, bool activo)? ObtenerPorCodigo(string codigo)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(
                "SELECT id, nombre, activo FROM productos WHERE codigo_interno = @cod", conn);
            cmd.Parameters.AddWithValue("@cod", codigo);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return (r.GetInt32(0), r.GetString(1), r.GetBoolean(2));
        }

        public string GenerarSiguienteCodigo(string prefijo) =>
            $"{prefijo}-{(ObtenerUltimoNumero(prefijo) + 1):D3}";

        public bool VarianteTieneVentas(int varianteId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM detalle_ventas WHERE variante_id = @id
                ) THEN 1 ELSE 0 END", conn);
            cmd.Parameters.AddWithValue("@id", varianteId);

            return (int)cmd.ExecuteScalar() == 1;
        }

        /// <summary>Variantes activas por debajo del stock mínimo, las sin stock primero.</summary>
        public List<FilaInventario> ObtenerVariantesCriticas()
        {
            var lista = new List<FilaInventario>();
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT p.id, p.codigo_interno, p.nombre, p.marca, c.nombre,
                       v.id, v.medida, v.precio_unitario, v.stock_actual, v.stock_minimo
                FROM variantes_producto v
                INNER JOIN productos p ON p.id = v.producto_id
                LEFT JOIN categorias c ON c.id = p.categoria_id
                WHERE p.activo = 1 AND v.stock_actual < v.stock_minimo
                ORDER BY CASE WHEN v.stock_actual <= 0 THEN 0 ELSE 1 END,
                         CAST(v.stock_actual AS float) / NULLIF(v.stock_minimo, 0),
                         p.nombre", conn);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new FilaInventario
                {
                    ProductoId = r.GetInt32(0),
                    CodigoInterno = r.GetString(1),
                    Nombre = r.GetString(2),
                    Marca = r.IsDBNull(3) ? null : r.GetString(3),
                    NombreCategoria = r.IsDBNull(4) ? null : r.GetString(4),
                    Activo = true,
                    VarianteId = r.GetInt32(5),
                    Medida = r.IsDBNull(6) ? null : r.GetString(6),
                    PrecioUnitario = r.GetDecimal(7),
                    StockActual = r.GetInt32(8),
                    StockMinimo = r.GetInt32(9),
                });
            }
            return lista;
        }

        public void EliminarProducto(int productoId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmdVar = new SqlCommand("DELETE FROM variantes_producto WHERE producto_id = @id", conn, tx);
                cmdVar.Parameters.AddWithValue("@id", productoId);
                cmdVar.ExecuteNonQuery();

                var cmdProd = new SqlCommand("DELETE FROM productos WHERE id = @id", conn, tx);
                cmdProd.Parameters.AddWithValue("@id", productoId);
                cmdProd.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}