using Microsoft.Data.SqlClient;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.Data
{
    public class ProductoRepository
    {
        public List<Producto> ObtenerPaginado(string busqueda, int? categoriaId,
                                               int pagina, int porPagina,
                                               out int totalRegistros)
        {
            var lista = new List<Producto>();
            totalRegistros = 0;

            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(busqueda))
                where += " AND (p.nombre LIKE @busqueda OR p.codigo_interno LIKE @busqueda OR c.nombre LIKE @busqueda)";
            if (categoriaId.HasValue)
                where += " AND p.categoria_id = @catId";

            var cmdCount = new SqlCommand($@"
                SELECT COUNT(DISTINCT p.id)
                FROM productos p
                LEFT JOIN categorias c ON c.id = p.categoria_id
                {where}", conn);

            AgregarParametrosBusqueda(cmdCount, busqueda, categoriaId);
            totalRegistros = (int)cmdCount.ExecuteScalar();

            var offset = (pagina - 1) * porPagina;
            var cmdData = new SqlCommand($@"
                SELECT p.id, p.codigo_interno, p.nombre, p.marca,
                       p.categoria_id, c.nombre AS nombre_categoria,
                       p.proveedor,
                       v.id AS var_id, v.medida, v.calidad,
                       v.precio_unitario, v.stock_actual, v.stock_minimo
                FROM productos p
                LEFT JOIN categorias c ON c.id = p.categoria_id
                LEFT JOIN variantes_producto v ON v.producto_id = p.id
                {where}
                ORDER BY p.id
                OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY", conn);

            AgregarParametrosBusqueda(cmdData, busqueda, categoriaId);
            cmdData.Parameters.AddWithValue("@offset", offset);
            cmdData.Parameters.AddWithValue("@porPagina", porPagina);

            using var reader = cmdData.ExecuteReader();
            var diccionario = new Dictionary<int, Producto>();

            while (reader.Read())
            {
                var pid = reader.GetInt32(reader.GetOrdinal("id"));
                if (!diccionario.TryGetValue(pid, out var prod))
                {
                    prod = new Producto
                    {
                        Id = pid,
                        CodigoInterno = reader.GetString(reader.GetOrdinal("codigo_interno")),
                        Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                        Marca = reader.IsDBNull(reader.GetOrdinal("marca")) ? null : reader.GetString(reader.GetOrdinal("marca")),
                        CategoriaId = reader.IsDBNull(reader.GetOrdinal("categoria_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("categoria_id")),
                        NombreCategoria = reader.IsDBNull(reader.GetOrdinal("nombre_categoria")) ? null : reader.GetString(reader.GetOrdinal("nombre_categoria")),
                        Proveedor = reader.IsDBNull(reader.GetOrdinal("proveedor")) ? null : reader.GetString(reader.GetOrdinal("proveedor")),
                    };
                    diccionario[pid] = prod;
                    lista.Add(prod);
                }

                if (!reader.IsDBNull(reader.GetOrdinal("var_id")))
                {
                    prod.Variantes.Add(new VarianteProducto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("var_id")),
                        ProductoId = pid,
                        Medida = reader.IsDBNull(reader.GetOrdinal("medida")) ? null : reader.GetString(reader.GetOrdinal("medida")),
                        Calidad = reader.IsDBNull(reader.GetOrdinal("calidad")) ? null : reader.GetString(reader.GetOrdinal("calidad")),
                        PrecioUnitario = reader.GetDecimal(reader.GetOrdinal("precio_unitario")),
                        StockActual = reader.GetInt32(reader.GetOrdinal("stock_actual")),
                        StockMinimo = reader.GetInt32(reader.GetOrdinal("stock_minimo")),
                    });
                }
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
                       p.proveedor,
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
                LEFT JOIN variantes_producto v ON v.producto_id = p.id", conn);

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

            var cmd = new SqlCommand("SELECT id, nombre FROM categorias ORDER BY nombre", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new Categoria { Id = r.GetInt32(0), Nombre = r.GetString(1) });

            return lista;
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
                    stock_minimo    = @smin
                WHERE id = @id",
                conn, tx);

            cmd.Parameters.AddWithValue("@id", v.Id);
            cmd.Parameters.AddWithValue("@medida", (object?)v.Medida ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@calidad", (object?)v.Calidad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@precio", v.PrecioUnitario);
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
    }
}