<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>
<!--#include file="Connections/INVFlood.asp" -->
<%
Response.CodePage = 874
Response.CharSet = "windows-874"
Session.CodePage = 874

Dim st
st = Request.QueryString("status")
If st = "" Then st = "red"   ' ค่าเริ่มต้น = ต้องสั่งซื้อ
%>

<!DOCTYPE html>
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>รายงานสถานะยา</title>

<style>
@page {
    size: A4;
    margin: 10mm;
}

body { 
    font-family: Tahoma, Arial, sans-serif;
    font-size:12px; 
    color:#000;
}

h2 {
    text-align:center;
    margin-bottom:8px;
    font-size:16px;
    font-weight:bold;
}

.table {
    width:100%;
    border-collapse:collapse;
    font-size:12px;
    font-family:Tahoma, Arial;
}

.table th, .table td {
    border:1px solid #000;
    padding:5px;
}

.table th {
    background:#d9e7ff; 
    font-weight:bold;
}

/* เพิ่มเติมเพื่อให้ภาษาไทยไม่แตกเมื่อพิมพ์ */
@media print {
    body, table, th, td {
        font-family: Tahoma !important;
        -webkit-print-color-adjust: exact;
    }
    .no-print { display:none; }
}
</style>
</head>
<body>

<div class="no-print" align="center" style="margin-bottom:10px;">
    <button onclick="window.print()">?? พิมพ์</button>
    <a href="INV_Report_Purchase.asp?status=<%=st%>" class="btn">? กลับหน้า Dashboard</a>
</div>

<h2>รายงานสถานะยา :
<%
Select Case st
    Case "red":   Response.Write("ต้องสั่งซื้อ")
    Case "yellow":Response.Write("ใกล้ถึงจุดสั่งซื้อ")
    Case "green": Response.Write("มีสำรอง")
End Select
%>
</h2>

<table class="table">
<thead>
<tr>
    <th width="6%">ลำดับ</th>
    <th width="13%">รหัสยา</th>
    <th width="46%">ชื่อยา</th>
    <th width="9%">คงคลัง</th>
    <th width="9%">MIN</th>
    <th width="9%">REORDER</th>
    <th width="8%">MAX</th>
</tr>
</thead>
<tbody>

<%
Function ToNumSoft(v)
    If IsNull(v) Or Trim(v & "") = "" Then ToNumSoft = 0 Else ToNumSoft = CDbl(v)
End Function

Dim sql, rs, i, showCount
i = 0 : showCount = 0

sql = "SELECT WORKING_CODE, DRUG_NAME, QTY_ON_HAND, MIN_LEVEL, MAX_LEVEL, REORDER_QTY " & _
      "FROM INV_MD WHERE (NOUSE IS NULL OR NOUSE='') AND (OUT_OF_LIST IS NULL OR OUT_OF_LIST='')" & _
      "ORDER BY WORKING_CODE"

Set rs = Server.CreateObject("ADODB.Recordset")
rs.Open sql, MM_INVFlood_STRING, 1, 1

If rs.EOF Then
%>
<tr><td colspan="7" align="center" style="color:red;">ไม่มีข้อมูลคลัง</td></tr>
<%
Else

Do While Not rs.EOF

    stock   = ToNumSoft(rs("QTY_ON_HAND"))
    minLv   = ToNumSoft(rs("MIN_LEVEL"))
    maxLv   = ToNumSoft(rs("MAX_LEVEL"))
    reorder = ToNumSoft(rs("REORDER_QTY"))

    ' กรณี REORDER ไม่ถูกกำหนด
    If reorder = 0 Then reorder = minLv

    If stock < minLv Then 
        statusKey = "red"
    ElseIf stock >= minLv And stock < reorder Then
        statusKey = "yellow"
    Else
        statusKey = "green"
    End If

    If st = statusKey Then
        i = i + 1
        showCount = showCount + 1
%>
<tr>
    <td align="center"><%=i%></td>
    <td align="center"><%=rs("WORKING_CODE")%></td>
    <td><%=rs("DRUG_NAME")%></td>
    <td align="center"><%=stock%></td>
    <td align="center"><%=minLv%></td>
    <td align="center"><%=reorder%></td>
    <td align="center"><%=maxLv%></td>
</tr>
<%
    End If

    rs.MoveNext
Loop

If showCount = 0 Then
%>
<tr><td colspan="7" align="center">ไม่มีรายการตามสถานะที่เลือก</td></tr>
<%
End If

End If

rs.Close
Set rs = Nothing
%>

</tbody>
</table>

<script> window.print(); </script>

</body>
</html>
