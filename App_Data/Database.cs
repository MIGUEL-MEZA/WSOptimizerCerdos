using System.Diagnostics;

namespace WSOptimizer7.App_Data
{
    using System;
    using Microsoft.VisualBasic;
    using System.Data;
    using System.Text.RegularExpressions;
    using System.Data.OleDb;
    using WSOptimizer7.Config;

    public class Database
    {
        public static string FormatDate(ref string strTexto)
        {
            string StrFinal = "";

            string mes = Strings.Mid(strTexto, 4, 2);
            string dia = Strings.Left(strTexto, 2);
            string anio = Strings.Right(strTexto, 4);

            StrFinal = mes + "/" + dia + "/" + anio;

            return StrFinal;
        }


        public static string FormatSQL(string strTexto)
        {
            

                strTexto = Strings.Replace(strTexto, "--", "");
            strTexto = Strings.Replace(strTexto, "[", "");
            strTexto = Strings.Replace(strTexto, "_", "_");
            strTexto = Strings.Replace(strTexto, "/*", "");
            strTexto = Strings.Replace(strTexto, "*/", "");
            strTexto = Strings.Replace(strTexto, "**", "");
            strTexto = Strings.Replace(strTexto, "#", "");
            if (String.IsNullOrEmpty(strTexto))
            {
                strTexto = Regex.Replace(strTexto, "set ", "set_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "where ", "where_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Create ", "create_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Insert ", "insert_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Select ", "select_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Values ", "values_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Table ", "table_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Declare ", "declare_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Alter ", "alter_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "Delete ", "delete_ ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "<script", "_script ", RegexOptions.IgnoreCase);
                strTexto = Regex.Replace(strTexto, "http", "http", RegexOptions.IgnoreCase); // STM: Para que quede en 
            }


            string[] arrValida;
            string strValida;
            strValida = ".JS #/*#ALTER #AUX#CLOCK$#COM1#COM8#CONFIG$#CREATE #DECLARE #DELETE #DROP #EXEC #INSERT #NULL #PRN#SCRIPT #SELECT #SET #SP_#SYS #VALUES #XP_";
            arrValida = strValida.Split("#");

            // For Each strV As String In arrValida
            // If InStr(LCase(strTexto), LCase(strV)) Then
            // Throw New System.Exception("Sentencia NO valida " + strV)
            // End If
            // Next
            if (strTexto == null)
                return "";
            return strTexto;
        }

        public static DataTable execQuery(string strSQL)
        {
            OleDbConnection Conn;
            Conn = RegresaBD();
            DataTable Table = new DataTable();
            try
            {
                Conn.Open();
                var DataAdapter1 = new System.Data.OleDb.OleDbDataAdapter(strSQL, Conn);
                var DataSet1 = new DataSet();
                DataAdapter1.Fill(DataSet1);
                Table = DataSet1.Tables[0];
                return Table;
            }
            catch (Exception ex)
            {
                // Throw ex
                throw new Exception("--execQuery--" + ex.Message + "--" + strSQL);
            }
            finally
            {
                Conn.Close();
            }
        }
        public static string execUpd(string strSQL)
        {
            OleDbConnection Conn;
            DataTable Tabla = new DataTable();
            System.Data.OleDb.OleDbCommand cmd;
            int resp;
            Conn = RegresaBD();
            object  idE ;
            try
            {
                Conn.Open();
                cmd = new System.Data.OleDb.OleDbCommand(strSQL, Conn);

                resp = cmd.ExecuteNonQuery();
                cmd.CommandText = "SELECT @@Identity ";
                idE = cmd.ExecuteScalar();
                return idE.ToString();
            }

            catch (Exception ex)
            {
                // Throw ex
                throw new Exception("--execUpd--" + ex.Message + "--" + strSQL);
            }
            finally
            {
                Conn.Close();
            }
        }

        public static int execNonQuery(string strSQL, string tipoDB = "")
        {
            System.Data.OleDb.OleDbCommand cmd;
            int resp ;
            OleDbConnection Conn;
            Conn = RegresaBD();
            try
            {
                Conn.Open();
                cmd = new System.Data.OleDb.OleDbCommand(strSQL, Conn);
                cmd.CommandTimeout = 0;
                resp = cmd.ExecuteNonQuery();
                return resp;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Conn.Close();
            }
        }

        /// <summary>
        ///     ''' Función que regresa la cadena de conexión dependiendo si me conecto a una base de access o sql
        ///     ''' </summary>
        ///     ''' <returns>Cadena de conexión SQL o Acess</returns>
        ///     ''' <remarks></remarks>
        private static OleDbConnection RegresaBD()
        {
            string str1 = "";
            str1 = AppSetConfig.AppSetting["ConnectionStrings:ConnBD"];
            OleDbConnection Oledbconn1 = new OleDbConnection(str1);
            return Oledbconn1;
        }
    }

}
