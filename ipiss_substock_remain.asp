<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")

sql = "SELECT DEPT_GR_NAME, DEPT_NAME, Sum(TOTAL_VALUE) AS Sumtotal from DEPT_ID d inner join SUBSTOCK s on d.DEPT_ID=s.DEPT_ID inner join DEPT_GROUP g on d.DEPT_GR_CODE = g.DEPT_GR_CODE GROUP BY DEPT_GR_NAME, DEPT_NAME order by DEPT_GR_NAME, DEPT_NAME"

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
    <th rowspan="2"><div align="center">หน่วยงาน</div></th>
    <th rowspan="2"><div align="center">สถานที่เก็บ</div></th>
	<th rowspan="2"><div align="center">มูลค่าคงคลัง</div></th>
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While NOT Drug.EOF
%>
	
    <tr> 

		<td><div align="left"><%if Drug("DEPT_GR_NAME") <> GR_NAME then response.write(Drug("DEPT_GR_NAME")) end if %></div></td>
		<td><div align="left"><%=Drug("DEPT_NAME")%></div></td>
		<td><div align="right"><%=formatnumber(Drug("Sumtotal"))%></div></td>
		      
<tr>
   
<%
  GR_NAME = Drug("DEPT_GR_NAME")
  runno = runno +1
  Drug.MoveNext()
Wend
%>
	</tbody>
</table>