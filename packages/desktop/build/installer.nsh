; Bastion's additions to electron-builder's Windows installer (NSIS).
;
; The host's game listens for the partner (TCP) and the start screen listens
; for hosts announcing themselves on the network (UDP): Windows Firewall would
; ask on the first launch. Installed "for all users" (the installer is then
; elevated), a rule lets Bastion.exe in on private networks and nobody is
; asked. Installed "only for me" there are no rights to add it: Windows asks
; once on the first launch, as with the zip, and "Allow" on private networks
; does the same.

!macro customInstall
  ${if} $installMode == "all"
    ; A reinstall replaces the rule instead of adding a second one.
    nsExec::ExecToLog 'netsh advfirewall firewall delete rule name="Bastion" program="$INSTDIR\Bastion.exe"'
    Pop $0
    nsExec::ExecToLog 'netsh advfirewall firewall add rule name="Bastion" description="Partidas de Bastion en la red local" dir=in action=allow program="$INSTDIR\Bastion.exe" enable=yes profile=private'
    Pop $0
    DetailPrint "Regla del cortafuegos para Bastion.exe: $0 (0 = anadida)"
  ${endif}
!macroend

!macro customUnInstall
  ${if} $installMode == "all"
    nsExec::ExecToLog 'netsh advfirewall firewall delete rule name="Bastion" program="$INSTDIR\Bastion.exe"'
    Pop $0
  ${endif}
!macroend
