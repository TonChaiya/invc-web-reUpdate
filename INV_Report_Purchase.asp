<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>
<!--#include file="Connections/INVFlood.asp" -->
<%
Response.CodePage = 874
Response.CharSet = "windows-874"
Session.CodePage = 874

Dim st
st = Request.QueryString("status")
If st = "" Then st = "red"   ' ? Default หมวดต้องสั่งซื้อ
%>

<!DOCTYPE html>
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>Dashboard สถานะยาที่ต้องสั่งซื้อ</title>
<link rel="stylesheet" href="css/bootstrap.min.css">

<style>
body { background:#f4f6f9; font-family:tahoma; font-size:13px; }
.table thead { background:#e9f1ff; }
.status-red  {background:#dc3545;color:#fff;padding:4px 8px;border-radius:8px;}
.status-yellow{background:#ffc107;color:#000;padding:4px 8px;border-radius:8px;}
.status-green {background:#28a745;color:#fff;padding:4px 8px;border-radius:8px;}
.page-header{font-size:21px;font-weight:bold;margin-top:10px;color:#003366;}
.btn-outline-danger,.btn-outline-warning,.btn-outline-success{background:#fff;}
</style>
</head>
<body>

<!-- เมนู -->
<!--#include file="menu.asp" -->

<%
Function ToNumSoft(v)
    If IsNull(v) Or Trim(v & "") = "" Then
        ToNumSoft = 0
    Else
        v = Trim(Replace(CStr(v), ",", ""))
        If IsNumeric(v) Then ToNumSoft = CDbl(v) Else ToNumSoft = 0
    End If
End Function

Function RoundUpASP(v)
    If Not IsNumeric(v) Then RoundUpASP = 0 Else RoundUpASP = -Int(-CDbl(v))
End Function
%>

<div class="container-fluid">

    <div class="page-header" align="center">Dashboard สถานะยาที่ต้องสั่งซื้อ</div>

    <!-- ปุ่มฟิลเตอร์สถานะ -->
<div align="center" style="margin-bottom:10px;">
    <a href="?status=red" class="btn <% If st="red" Then Response.Write("btn-danger") Else Response.Write("btn-outline-danger") End If %>">ต้องสั่งซื้อ</a>
    <a href="?status=yellow" class="btn <% If st="yellow" Then Response.Write("btn-warning") Else Response.Write("btn-outline-warning") End If %>">ใกล้ถึงจุดสั่งซื้อ</a>
    <a href="?status=green" class="btn <% If st="green" Then Response.Write("btn-success") Else Response.Write("btn-outline-success") End If %>">มีสำรอง</a>
</div>

<div align="center" style="margin-bottom:15px;">
    <a href="INV_Report_Purchase_Print.asp?status=<%=st%>" target="_blank" class="btn btn-primary">
        พิมพ์รายงาน PDF
</a>

</div>

    

    <table class="table table-bordered table-condensed table-hover">
        <thead>
        <tr>
            <th>#</th>
            <th>รหัสยา</th>
            <th>ชื่อยา</th>
            <th>คงคลังจริง</th>
            <th>MIN</th>
            <th>REORDER</th>
            <th>MAX</th>
            <th>แนะนำสั่งซื้อ</th>
            <th>สถานะ</th>
        </tr>
        </thead>
        <tbody>

<%
Dim sql, rs, i, showCount
i = 0 : showCount = 0

' ? ตัด TRY_CAST ออก ใช้ ORDER BY ตัวอักษรแทน
sql = "SELECT WORKING_CODE, DRUG_NAME, QTY_ON_HAND, MIN_LEVEL, MAX_LEVEL, REORDER_QTY " & _
      "FROM INV_MD WHERE (NOUSE IS NULL OR NOUSE='') AND (OUT_OF_LIST IS NULL OR OUT_OF_LIST='')" & _
      "ORDER BY WORKING_CODE"

Set rs = Server.CreateObject("ADODB.Recordset")
rs.Open sql, MM_INVFlood_STRING, 1, 1

If rs.EOF Then
%>
<tr><td colspan="9" align="center" style="color:red;">ไม่มีข้อมูลคลัง</td></tr>
<%
Else

Do While Not rs.EOF

    stock   = ToNumSoft(rs("QTY_ON_HAND"))
    minLv   = ToNumSoft(rs("MIN_LEVEL"))
    maxLv   = ToNumSoft(rs("MAX_LEVEL"))
    reorder = ToNumSoft(rs("REORDER_QTY"))
    suggest = RoundUpASP(maxLv - stock)

If reorder = 0 Then reorder = minLv ' fallback ถ้าไม่ได้กำหนดในระบบ

If stock < minLv Then
    statusKey = "red"
    statusColor = "<span class='status-red'>ต้องสั่งซื้อทันที</span>"
ElseIf stock >= minLv And stock < reorder Then
    statusKey = "yellow"
    statusColor = "<span class='status-yellow'>ใกล้ถึงจุดสั่งซื้อ</span>"
Else
    statusKey = "green"
    statusColor = "<span class='status-green'>มีสำรอง</span>"
End If

    ' แสดงเฉพาะคีย์ที่เลือก
    If st = statusKey Then
        showCount = showCount + 1
        i = i + 1
%>

<tr>
    <td align="center"><%=i%></td>
    <td align="center"><%=rs("WORKING_CODE")%></td>
    <td><%=rs("DRUG_NAME")%></td>
    <td align="right"><%=stock%></td>
    <td align="right"><%=minLv%></td>
    <td align="right"><%=reorder%></td>
    <td align="right"><%=maxLv%></td>
    <td align="right"><%=suggest%></td>
    <td align="center"><%=statusColor%></td>
</tr>

<%
    End If

    rs.MoveNext
Loop

If showCount = 0 Then
%>
<tr>
<td colspan="9" align="center" style="color:red;">ไม่มีรายการตามสถานะที่เลือก</td>
</tr>
<%
End If

End If 'EOF check
%>

        </tbody>
    </table>
</div>

</body>
</html>
