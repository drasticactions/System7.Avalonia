        org 0
        dc.w 0,1
entry:
        pea -4(a5)
        dc.w $a86e
        dc.w $a8fe
        dc.w $a912
        dc.w $a930
        dc.w $a9cc
        clr.l -(sp)
        dc.w $a97b
        dc.w $a850
        dc.w $a852
        lea state(pc),a4
        clr.l -(sp)
        move.w #128,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        dc.w $a97c
        move.l (sp)+,(a4)
        move.l (a4),-(sp)
        dc.w $a873
        moveq #1,d3
        lea 20(a4),a3
save_handles:
        bsr item
        move.l 8(a4),(a3)+
        addq.w #1,d3
        cmp.w #6,d3
        ble save_handles
loop:
        clr.l -(sp)
        pea 4(a4)
        dc.w $a991
        cmp.w #1,4(a4)
        bne loop
        moveq #2,d3
        bsr item
        move.l 8(a4),-(sp)
        pea first_text(pc)
        dc.w $a98f
        moveq #3,d3
        bsr item
        move.l 8(a4),-(sp)
        pea second_text(pc)
        dc.w $a98f
        moveq #4,d3
        bsr item
        move.l 8(a4),-(sp)
        pea multiline_text(pc)
        dc.w $a98f
        move.l (a4),-(sp)
        move.w #2,-(sp)
        clr.w -(sp)
        clr.w -(sp)
        dc.w $a97e
        bra loop
item:
        move.l (a4),-(sp)
        move.w d3,-(sp)
        pea 6(a4)
        pea 8(a4)
        pea 12(a4)
        dc.w $a98d
        rts
first_text:
        dc.b 9,'Macintosh'
second_text:
        dc.b 8,'System 7'
multiline_text:
        dc.b multiline_end-multiline_text-1,'Select and edit this text.',13,'A second line.'
multiline_end:
        even
        dc.b 'S7TEXT!!'
state:
        ds.b 44
