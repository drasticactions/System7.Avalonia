        org 0
        dc.w 0,1
entry:
        pea -4(a5)
        dc.w $a86e
        dc.w $a8fe
        dc.w $a912
        dc.w $a930
        dc.w $a9cc
        dc.w $a850
        dc.w $a852
        lea state(pc),a4
        clr.l -(sp)
        move.w #128,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        dc.w $a9bd
        move.l (sp)+,(a4)
        move.l (a4),-(sp)
        dc.w $a873
        lea 4(a4),a3
        move.w #128,d3
create:
        clr.l -(sp)
        move.w d3,-(sp)
        move.l (a4),-(sp)
        dc.w $a9be
        move.l (sp)+,(a3)+
        addq.w #1,d3
        cmp.w #134,d3
        blt create
        move.l 8(a4),-(sp)
        move.w #2,-(sp)
        dc.w $a963
        move.l 12(a4),-(sp)
        move.w #255,-(sp)
        dc.w $a95d
        move.l 20(a4),-(sp)
        move.w #6,-(sp)
        dc.w $a963
loop:
        clr.w -(sp)
        move.w #$ffff,-(sp)
        pea 28(a4)
        dc.w $a970
        tst.b (sp)+
        beq loop
        move.w 28(a4),d0
        cmp.w #6,d0
        beq update
        cmp.w #3,d0
        beq key
        cmp.w #1,d0
        bne loop
        clr.w -(sp)
        move.l 38(a4),-(sp)
        pea 44(a4)
        dc.w $a92c
        cmp.w #3,(sp)+
        bne loop
        move.l (a4),-(sp)
        dc.w $a91f
        move.l (a4),-(sp)
        dc.w $a873
        move.l 38(a4),48(a4)
        pea 48(a4)
        dc.w $a871
        clr.w -(sp)
        move.l 48(a4),-(sp)
        move.l (a4),-(sp)
        pea 44(a4)
        dc.w $a96c
        move.w (sp)+,52(a4)
        beq loop
        clr.w -(sp)
        move.l 44(a4),-(sp)
        move.l 48(a4),-(sp)
        move.l #-1,-(sp)
        dc.w $a968
        move.w (sp)+,54(a4)
        addq.l #1,56(a4)
        bra loop
key:
        cmp.b #'d',33(a4)
        beq inactive
        cmp.b #'e',33(a4)
        beq disabled
        cmp.b #'a',33(a4)
        beq active
        cmp.b #'r',33(a4)
        beq reset
        cmp.b #'n',33(a4)
        beq narrow
        cmp.b #'w',33(a4)
        bne loop
        move.w #230,d0
        bra resize
narrow:
        move.w #140,d0
resize:
        move.l 4(a4),-(sp)
        move.w d0,-(sp)
        move.w #20,-(sp)
        dc.w $a95c
        bra loop
reset:
        move.l 4(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        bra loop
inactive:
        move.w #254,d0
        bra hilite
disabled:
        move.w #255,d0
        bra hilite
active:
        moveq #0,d0
hilite:
        move.l 4(a4),-(sp)
        move.w d0,-(sp)
        dc.w $a95d
        bra loop
update:
        move.l 30(a4),-(sp)
        dc.w $a922
        move.l (a4),-(sp)
        dc.w $a873
        move.l (a4),-(sp)
        dc.w $a969
        move.l 30(a4),-(sp)
        dc.w $a923
        bra loop
        dc.b 'S7POPUP!'
state:
        ds.b 60
