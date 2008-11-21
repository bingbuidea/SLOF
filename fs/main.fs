\ =============================================================================
\  * Copyright (c) 2004, 2005 IBM Corporation
\  * All rights reserved. 
\  * This program and the accompanying materials 
\  * are made available under the terms of the BSD License 
\  * which accompanies this distribution, and is available at
\  * http://www.opensource.org/licenses/bsd-license.php
\  * 
\  * Contributors:
\  *     IBM Corporation - initial implementation
\ =============================================================================


\ The master file.  Everything else is included into here.

hex

\ Speed up compiling.
INCLUDE find-hash.fs

\ Enable use of multiple wordlists and vocabularies.
INCLUDE search.fs

\ Heap memory allocation.
INCLUDE alloc-mem.fs

\ First some very generic code.
: d#  parse-word base @ >r decimal evaluate r> base ! ; immediate
: END-STRUCT  drop ;
: 0.r  0 swap <# 0 ?DO # LOOP #> type ;

: zcount ( zstr -- str len )  dup BEGIN dup c@ WHILE char+ REPEAT over - ;
: zplace ( str len buf -- )  2dup + 0 swap c! swap move ;

CREATE $catpad 100 allot
\ First input string is allowed to already be on the pad; second is not.
: $cat ( str1 len1 str2 len2 -- str3 len3 )
  >r >r dup >r $catpad swap move
  r> dup $catpad + r> swap r@ move
  r> + $catpad swap ;

: 2CONSTANT    CREATE swap , , DOES> 2@ ;
: $2CONSTANT  $CREATE swap , , DOES> 2@ ;

\ Memory and I/O hexdump.
INCLUDE dump.fs

\ I/O accesses.
INCLUDE hw/io.fs

\ Start the serial console.
INCLUDE hw/serial.fs

\ Input line editing.
INCLUDE accept.fs

\ Register frame layout.
STRUCT
  cell FIELD >r0   cell FIELD >r1   cell FIELD >r2   cell FIELD >r3
  cell FIELD >r4   cell FIELD >r5   cell FIELD >r6   cell FIELD >r7
  cell FIELD >r8   cell FIELD >r9   cell FIELD >r10  cell FIELD >r11
  cell FIELD >r12  cell FIELD >r13  cell FIELD >r14  cell FIELD >r15
  cell FIELD >r16  cell FIELD >r17  cell FIELD >r18  cell FIELD >r19
  cell FIELD >r20  cell FIELD >r21  cell FIELD >r22  cell FIELD >r23
  cell FIELD >r24  cell FIELD >r25  cell FIELD >r26  cell FIELD >r27
  cell FIELD >r28  cell FIELD >r29  cell FIELD >r30  cell FIELD >r31
  cell FIELD >cr   cell FIELD >xer  cell FIELD >lr   cell FIELD >ctr
  cell FIELD >srr0 cell FIELD >srr1 cell FIELD >dar  cell FIELD >dsisr
END-STRUCT

1100000 CONSTANT eregs  \ Exception register frame.
1100400 CONSTANT ciregs \ Client (interface) register frame.

\ Print out an exception frame, e.g.,   eregs .regs
: .16  10 0.r 3 spaces ;
: .8   8 spaces 8 0.r 3 spaces ;
: .4regs  cr 4 0 DO dup @ .16 8 cells+ LOOP drop ;
: .fixed-regs
  cr ."     R0 .. R7           R8 .. R15         R16 .. R23         R24 .. R31"
  dup 8 0 DO dup .4regs cell+ LOOP drop ;
: .special-regs
  cr ."     CR / XER           LR / CTR          SRR0 / SRR1        DAR / DSISR"
  cr dup >cr  @ .8   dup >lr  @ .16  dup >srr0 @ .16  dup >dar @ .16
  cr dup >xer @ .16  dup >ctr @ .16  dup >srr1 @ .16    >dsisr @ .8 ;
: .regs
  cr .fixed-regs
  cr .special-regs
  cr cr ;

\ Some low-level functions -- no source code provided, sorry.
: reboot  0 1 oco ;
: halt  0 2 oco ;
: watchdog  3 oco ;
: other-firmware  1 watchdog ;

\ Get the second CPU into our own spinloop.
0 VALUE slave?
0 7 oco CONSTANT master-cpu
cr .( The master cpu is #) master-cpu .
: get-slave ( addr -- )  0 oco 1 = IF true to slave? THEN ;
: slave-report  cr slave? IF ." Second CPU is running." ELSE
                             ." Second CPU is NOT running!" THEN ;
3f00 get-slave  slave-report

\ Packages, instances, properties, devices, methods -- the whole shebang.
INCLUDE package.fs

\ Environment variables.  Not actually used right now.
INCLUDE envvar.fs

\ Hook to help loading our secondary boot loader.
DEFER disk-read ( lba cnt addr -- )

\ Timebase frequency, in Hz.
-1 VALUE tb-frequency
-1 VALUE cpu-frequency

\ The device tree.
INCLUDE js20-tree.fs

\ The client interface.
INCLUDE client.fs

\ ELF binary file format.
INCLUDE elf.fs

\ Give our client a stack.
111f000 ciregs >r1 !

\ Run the client program.
: start-elf ( entry-addr -- )  msr@ 7fffffffffffffff and 2000 or ciregs >srr1 !
                               0 0 rot call-client ;
: start-elf64 ( entry-addr -- )  msr@ 2000 or ciregs >srr1 !
                                 0 0 rot call-client ;

\ Where files are loaded.  Not the same as where they are executed.
10000 CONSTANT load-base
\ Load secondary boot loader from disk @ sector 63.  512kB should be enough.
: read-yaboot  3f 400 load-base disk-read ;

: yaboot 
  cr ." Reading..." read-yaboot
  ." relocating..." load-base load-elf-file
  ." go!" start-elf ;

: set-bootpart ( -- ) 
  skipws 0 parse s" disk:" 2swap $cat 
  encode-string s" bootpath" set-chosen ;

: set-bootargs 
  skipws 0 parse dup 0= IF 2drop ELSE
  encode-string s" bootargs" set-chosen THEN ;
: etherboot  s" enet:" set-bootargs payload dup load-elf-file start-elf ;
: go  etherboot ;

\ Default bootpath

s" disk:3" encode-string s" bootpath" set-chosen

: boot set-bootargs yaboot ;

: auto
  key? IF clear c emit key drop QUIT THEN
  cr ." Netboot -- to drop into GUI instead, please hold a key while starting"
  cr
  payload dup load-elf-file start-elf ;

: disable-boot-watchdog
  0 watchdog IF cr ." Failed to disable boot watchdog." THEN ;

disable-boot-watchdog

cr
cr
cr .( SLOF version 0.0  Copyright 2004,2005 IBM Corporation)
cr .( Part of this code is:)
cr
cr .( Licensed Internal Code - Property of IBM)
cr .( JS20 Licensed Internal Code)
cr .( (C) char ) emit .(  Copyright IBM Corp. 2004, 2005 All Rights Reserved.)
cr .( US Government Users Restricted Rights - Use, duplication or)
cr .( disclosure restricted by GSA ADP Schedule Contract with IBM)
cr
cr

\ Enable this if you want your system to automatically boot by default:
\   auto
