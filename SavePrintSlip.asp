<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

if Session("UserID") <> "" then 

    Set SMPO = Server.CreateObject("ADODB.Recordset")
    SMPO.ActiveConnection = MM_INVFlood_STRING

    SUB_PO_NO = request.form("SUB_PO_NO")

    SMPO.Source = "SELECT ACC_NO FROM SM_PO WHERE SUB_PO_NO='" & SUB_PO_NO & "'"
    SMPO.CursorType = 2
    SMPO.CursorLocation = 2
    SMPO.LockType = 3
    SMPO.Open()

    If not SMPO.eof then

      SMPO("ACC_NO") = Session("UserID") 
      SMPO.update
      response.write("success")
    else 
      response.write("failed")
    end if

Response.charset="windows-874"

end if 

%>

