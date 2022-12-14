using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace CompareDb
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
            txtBaseServer.Text = "THUANVIETSOFT\\SQLSERVER";
            txtBaseUsername.Text = "sa";
            txtBasePassword.Text = "1433";
            txtBaseDatabase.Text = "DB_UPDATE";

            txtCompareServer.Text = "THUANVIETSOFT\\SQLSERVER";
            txtCompareUsername.Text = "sa";
            txtComparePassword.Text = "1433";
            txtCompareDatabase.Text = "BENXE";

            btnThucHien.Click += new EventHandler(btnThucHien_Click);
        }

        void btnThucHien_Click(object sender, EventArgs e)
        {
            Database dbBase = null, dbCompare = null;
            //kiểm tra kết nối
            if (KiemTraKetNoi(ref dbBase, ref dbCompare))
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("TABLE", typeof(string));
                dt.Columns.Add("COLUMN", typeof(string));
                dt.Columns.Add("NOTE", typeof(string));

                StringBuilder sbQuery = new StringBuilder();

                //table (chỉ lấy bảng D và T)
                DataTable dtBaseTable = dbBase.GetTable("SELECT Name FROM sys.tables ORDER BY Name");
                DataTable dtCompareTable = dbBase.GetTable("SELECT Name FROM sys.tables ORDER BY Name");

                //thừa, thiếu, sai kiểu dữ liệu
                foreach (DataRow row in dtBaseTable.Rows)
                {
                    string TableName = row["Name"].ToString();
                    DataRow[] rowTabales = dtCompareTable.Select(string.Format("Name='{0}'", TableName));
                    if (rowTabales.Length == 0) return;

                    string queryCol = @"select *, (select is_identity from sys.columns C where C.name = S.name and C.object_id = S.id) as is_identity
                    from syscolumns S where id = object_id('" + TableName + "')";

                    DataTable dtBaseColumn = dbBase.GetTable(queryCol);
                    DataTable dtCompareColumn = dbCompare.GetTable(queryCol);

                    foreach (DataRow rowColBase in dtBaseColumn.Rows)
                    {
                        string colName = rowColBase["name"].ToString();
                        DataRow[] rowColumns = dtCompareColumn.Select(string.Format("name='{0}'", colName));
                        if (rowColumns.Length > 0)
                        {
                            //kiểm tra kiểu dữ liệu
                            string typeBaseColumn = GetXtype(rowColBase);
                            string typeCompareColumn = GetXtype(rowColumns[0]);

                            //Khác kiểu ký tự
                            string collationBase = rowColBase["collation"].ToString();
                            string collationCompare = rowColumns[0]["collation"].ToString();
                            if (collationBase.ToLower() != collationCompare.ToLower())
                            {
                                AddRowCollation(dt, sbQuery, TableName, colName, typeCompareColumn, collationBase, collationCompare);
                            }
                            else
                            {
                                if (typeBaseColumn.ToLower() != typeCompareColumn.ToLower())
                                {
                                    //Khác kiểu dữ liệu
                                    InsertRowEdit(dt, sbQuery, TableName, colName, typeBaseColumn, typeCompareColumn);
                                }
                            }
                        }
                        else
                        {
                            //thừa
                            InsertRowAddOrRemve(dt, sbQuery, TableName, colName, "", "");
                        }
                    }

                    foreach (DataRow rowColCompare in dtCompareColumn.Rows)
                    {
                        string colName = rowColCompare["name"].ToString();
                        DataRow[] rowColumns = dtBaseColumn.Select(string.Format("Name='{0}'", colName));
                        if (rowColumns.Length == 0)
                        {
                            //thiếu
                            InsertRowAddOrRemve(dt, sbQuery, TableName, colName, GetXtype(rowColCompare), rowColCompare["collation"].ToString());
                        }
                    }
                }

                detail.DataSource = dt;
                txtUpdate.Text = sbQuery.ToString().Trim();

                if (dt.Rows.Count == 0)
                {
                    ShowInfo("Dữ liệu đã đồng nhất");
                }
            }
        }

        private void AddRowCollation(DataTable dt, StringBuilder sbQuery, string TableName, string ColumName, string typeBaseColumn, string collationBase, string collationCompare)
        {
            DataRow row = dt.NewRow();
            row["TABLE"] = TableName;
            row["COLUMN"] = ColumName;
            row["NOTE"] = string.Format("Khác kiểu ký tự {0} # {1}", collationBase, collationCompare);
            dt.Rows.Add(row);

            //Sinh Sql
            string query = string.Format("ALTER TABLE {0} ALTER COLUMN {1} {2} COLLATE {3}", TableName, ColumName, typeBaseColumn, collationCompare);

            if (sbQuery.ToString().Length > 0) sbQuery.AppendLine("GO;");
            sbQuery.AppendLine(query);
        }

        private void InsertRowEdit(DataTable dt, StringBuilder sbQuery, string TableName, string ColumName, string typeBaseColumn, string typeCompareColumn)
        {
            DataRow row = dt.NewRow();
            row["TABLE"] = TableName;
            row["COLUMN"] = ColumName;
            row["NOTE"] = string.Format("Khác kiểu dữ liệu {0} # {1}", typeBaseColumn, typeCompareColumn);
            dt.Rows.Add(row);

            //Sinh Sql
            string query = string.Format("ALTER TABLE {0} ALTER COLUMN {1} {2}", TableName, ColumName, typeCompareColumn);

            if (sbQuery.ToString().Length > 0) sbQuery.AppendLine("GO;");
            sbQuery.AppendLine(query);
        }

        private void InsertRowAddOrRemve(DataTable dt, StringBuilder sbQuery, string TableName, string ColumName, string typeOfColumn, string collation)
        {
            bool ThieuCot = (typeOfColumn != null && typeOfColumn.Length > 0);

            DataRow row = dt.NewRow();
            row["TABLE"] = TableName;
            row["COLUMN"] = ColumName;
            row["NOTE"] = ThieuCot ? "Thiếu" : "Thừa";
            dt.Rows.Add(row);

            //Sinh Sql
            string query = "";
            if (ThieuCot)
            {
                query = string.Format("ALTER TABLE {0} ADD {1} {2} COLLATE {3} ", TableName, ColumName, typeOfColumn, collation);
            }
            else
            {
                query = string.Format("ALTER TABLE {0} DROP COLUMN {1}", TableName, ColumName);
            }

            if (sbQuery.ToString().Length > 0) sbQuery.AppendLine("GO;");
            sbQuery.AppendLine(query);
        }

        private string GetXtype(DataRow rowColBase)
        {
            XType xType = ((XType)int.Parse(rowColBase["xtype"].ToString()));
            string type = xType.ToString();
            switch (xType)
            {
                case XType.Decimal:
                    type = type + "(" + rowColBase["prec"].ToString() + "," + rowColBase["scale"].ToString() + ")";
                    break;
                case XType.Varchar:
                case XType.Char:
                case XType.Nvarchar:
                case XType.Nchar:
                case XType.Varbinary:
                    if (rowColBase["prec"].ToString() == "-1")
                        type = type + "(MAX)";
                    else
                        type = type + "(" + rowColBase["prec"].ToString() + ")";
                    break;
            }
            return type;
        }

        private bool KiemTraKetNoi(ref Database dbBase, ref Database dbCompare)
        {
            dbBase = new Database(txtBaseServer.Text.Trim(), txtBaseUsername.Text.Trim(), txtBasePassword.Text.Trim(), txtBaseDatabase.Text.Trim());
            dbCompare = new Database(txtCompareServer.Text.Trim(), txtCompareUsername.Text.Trim(), txtComparePassword.Text.Trim(), txtCompareDatabase.Text.Trim());
            string error = "";
            if (!dbBase.TryConnect(out error))
            {
                error = "Lỗi kết nối dữ liệu cần so sánh: " + error;
                ShowWarning(error);
                return false;
            }
            if (!dbCompare.TryConnect(out error))
            {
                error = "Lỗi kết nối dữ liệu cần so sánh: " + error;
                ShowWarning(error);
                return false;
            }
            return true;
        }

        void ShowWarning(string msg)
        {
            MessageBox.Show(msg, "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void ShowInfo(string msg)
        {
            MessageBox.Show(msg, "Thông tin", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public enum XType
    {
        Image = 34,
        Text = 35,
        Uniqueidentifier = 36,
        Tinyint = 48,
        Smallint = 52,
        Int = 56,
        Smalldatetime = 58,
        Real = 59,
        Money = 60,
        Datetime = 61,
        Float = 62,
        Sql_variant = 98,
        Ntext = 99,
        Bit = 104,
        Decimal = 106,
        Numeric = 108,
        Smallmoney = 122,
        Bigint = 127,
        Varbinary = 165,
        Varchar = 167,
        Binary = 173,
        Char = 175,
        Timestamp = 189,
        Nvarchar = 231,
        Sysname = 231,
        Nchar = 239,
        Xml = 241,
    }

    public class Database
    {
        public Database(string server, string username, string password, string database)
        {
            this.server = server;
            this.username = username;
            this.password = password;
            this.database = database;
        }

        public string server;
        public string username;
        public string password;
        public string database;

        private SqlConnection Connection()
        {
            string connStr = string.Format("Data Source={0};Initial Catalog={1};User id={2};Password={3};", server, database, username, password);
            SqlConnection conn = new SqlConnection(connStr);
            return conn;
        }

        public bool TryConnect(out string error)
        {
            error = "";
            SqlConnection conn = null;
            try
            {
                conn = Connection();
                conn.Open();
                return true;
            }
            catch (Exception ex)
            {
                return false;
                error = ex.Message;
            }
            finally
            {
                if (conn != null && conn.State == ConnectionState.Open)
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public DataTable GetTable(string query)
        {
            DataTable dt = new DataTable();
            SqlConnection conn = null;
            try
            {
                conn = Connection();
                conn.Open();
                SqlDataAdapter da = new SqlDataAdapter(query, conn);
                da.Fill(dt);
            }
            catch (Exception ex){}
            finally
            {
                if (conn != null && conn.State == ConnectionState.Open)
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
            return dt;
        }
    }
}