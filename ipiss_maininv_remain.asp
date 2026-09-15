<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")

sql = "SELECT TBLED_NED.EDNAME, Sum(INV_MD.TOTAL_VALUE) AS SumOfTOTAL_VALUE, count(TBLED_NED.EDNAME) as NoOfItem, TBLED_NED.EDCODE FROM INV_MD INNER JOIN TBLED_NED ON INV_MD.ED_NED = TBLED_NED.EDCODE WHERE NOUSE is NULL GROUP BY TBLED_NED.EDNAME, TBLED_NED.EDCODE ORDER BY TBLED_NED.EDCODE"

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.Open sql, Conn, 1,3



Function CVNum2P(NUM,Dec)

	If IsNull(NUM) = True Or NUM = "" Then
		CVNum2P = 0
	Else
		If Dec ="Y" Then
			CVNum2P = formatnumber(NUM)
		else
			CVNum2P = formatnumber(NUM,"0,000")
		End If
	End If

End Function


Response.charset="windows-874"
%>



<table class="table table-bordered table-sm" width="100%" cellspacing="0">
  <thead>
  <tr>
    <th rowspan="2"><div align="center">ประเภท</div></th>
    <th rowspan="2"><div align="center">จำนวนรายการ</div></th>
	<th rowspan="2"><div align="center">มูลค่าคงคลัง</div></th>
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While NOT Drug.EOF
%>
	
    <tr> 

		<td><div align="left"><%=runno & ". " & Drug("EDNAME")%></div></td>
		<td><div align="center"><%=Drug("NoOfItem")%></div></td>
		<td><div align="right"><%=formatnumber(Drug("SumOfTOTAL_VALUE"))%></div></td>
		      
<tr>
   
<%
  totalitem = totalitem + CDbl(Drug("NoOfItem"))
  totalvalue = totalvalue + CDbl(Drug("SumOfTOTAL_VALUE"))
  runno = runno +1
  Drug.MoveNext()
Wend
%>
<tr>
	<td><div align="center" class="style12"> <strong> รวม</strong></div></td>
	<td><div align="center" class="style12"> <strong> <%=formatnumber(totalitem,0)%></strong></div></td>
	<td><div align="right" class="style12"> <strong> <%=formatnumber(totalvalue,2)%></strong></div></td>
</tr>
	</tbody>
</table>