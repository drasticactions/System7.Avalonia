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
        ifd CONTROL_VARIANTS
        ifd COLOR
        clr.l -(sp)
        dc.w $aa2a
        move.l (sp)+,76(a4)
        moveq #32,d5
variant_depth:
        clr.w -(sp)
        move.l 76(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a14,d0
        dc.w $aaa2
        tst.w (sp)+
        bne set_variant_depth
        lsr.w #1,d5
        bne variant_depth
        bra variant_depth_ready
set_variant_depth:
        clr.w -(sp)
        move.l 76(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a13,d0
        dc.w $aaa2
        addq.l #2,sp
variant_depth_ready:
        endif
        endif
        clr.l -(sp)
        move.w #128,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        dc.w $a97c
        move.l (sp)+,(a4)
        move.l (a4),-(sp)
        dc.w $a873
        moveq #5,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        moveq #6,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        moveq #8,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        moveq #2,d3
        bsr disable
        moveq #6,d3
        bsr disable
        moveq #9,d3
        bsr disable
        moveq #13,d3
        bsr disable
        moveq #1,d3
        lea 20(a4),a3
save_handles:
        bsr item
        move.l 8(a4),(a3)+
        addq.w #1,d3
        cmp.w #14,d3
        ble save_handles
loop:
        clr.l -(sp)
        pea 4(a4)
        dc.w $a991
        move.w 4(a4),d3
        ifd MEMORY_SLIDER
        cmp.w #10,d3
        beq cycle_memory_slider
        endif
        ifd SOUND_SLIDER
        cmp.w #10,d3
        beq cycle_memory_slider
        endif
        ifd NETWORK_SLIDER
        cmp.w #10,d3
        beq cycle_memory_slider
        endif
        ifd SPEECH_SLIDER
        cmp.w #3,d3
        beq cycle_speech_slider
        endif
        ifd SPEECH_CUSTOM
        cmp.w #3,d3
        beq toggle_speech_disabled
        endif
        ifd SPEECH_BUTTONS
        cmp.w #1,d3
        beq cycle_speech_values
        cmp.w #3,d3
        beq toggle_speech_buttons_disabled
        endif
        cmp.w #3,d3
        beq reset_scroll
        cmp.w #4,d3
        blt loop
        cmp.w #9,d3
        bgt loop
        cmp.w #7,d3
        bge radio
        bsr item
        clr.w -(sp)
        move.l 8(a4),-(sp)
        dc.w $a960
        move.w (sp)+,d0
        eor.w #1,d0
        move.l 8(a4),-(sp)
        move.w d0,-(sp)
        dc.w $a963
        bra loop
reset_scroll:
        moveq #11,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #50,-(sp)
        dc.w $a963
        moveq #12,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #50,-(sp)
        dc.w $a963
        bra loop
radio:
        move.w d3,d4
        moveq #7,d3
        bsr item
        move.l 8(a4),-(sp)
        clr.w -(sp)
        dc.w $a963
        moveq #8,d3
        bsr item
        move.l 8(a4),-(sp)
        clr.w -(sp)
        dc.w $a963
        move.w d4,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        bra loop
        ifd MEMORY_SLIDER
cycle_memory_slider:
        addq.w #1,80(a4)
        cmp.w #3,80(a4)
        blt slider_value
        clr.w 80(a4)
slider_value:
        moveq #10,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w 80(a4),d0
        add.w d0,d0
        lea slider_values(pc),a0
        move.w (a0,d0.w),-(sp)
        dc.w $a963
        bra loop
slider_values:
        dc.w 1,64,126
        endif
        ifd SOUND_SLIDER
cycle_memory_slider:
        addq.w #1,80(a4)
        cmp.w #8,80(a4)
        blt slider_value
        clr.w 80(a4)
slider_value:
        moveq #10,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w 80(a4),-(sp)
        dc.w $a963
        bra loop
        endif
        ifd NETWORK_SLIDER
cycle_memory_slider:
        addq.w #1,80(a4)
        cmp.w #11,80(a4)
        blt slider_value
        clr.w 80(a4)
slider_value:
        moveq #10,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w 80(a4),-(sp)
        dc.w $a963
        bra loop
        endif
        ifd SPEECH_CUSTOM
toggle_speech_disabled:
        eori.w #255,80(a4)
        moveq #10,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w 80(a4),-(sp)
        dc.w $a95d
        moveq #11,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w 80(a4),-(sp)
        dc.w $a95d
        bra loop
        endif
        ifd SPEECH_BUTTONS
cycle_speech_values:
        addq.w #1,82(a4)
        cmp.w #4,82(a4)
        blt speech_value
        clr.w 82(a4)
speech_value:
        move.w 82(a4),d0
        add.w d0,d0
        lea speech_values(pc),a0
        move.w (a0,d0.w),d4
        moveq #10,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w d4,-(sp)
        dc.w $a963
        moveq #11,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w d4,-(sp)
        dc.w $a963
        bra loop
speech_values:
        dc.w 0,1,50,100
toggle_speech_buttons_disabled:
        eori.w #255,80(a4)
        moveq #10,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w 80(a4),-(sp)
        dc.w $a95d
        moveq #11,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w 80(a4),-(sp)
        dc.w $a95d
        bra loop
        endif
        ifd SPEECH_SLIDER
cycle_speech_slider:
        addq.w #1,80(a4)
        cmp.w #5,80(a4)
        blt speech_slider_value
        clr.w 80(a4)
speech_slider_value:
        move.w 80(a4),d0
        add.w d0,d0
        lea speech_slider_values(pc),a0
        move.w (a0,d0.w),d4
        moveq #10,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w d4,-(sp)
        dc.w $a963
        bra loop
speech_slider_values:
        dc.w 20,100,200,300,400
        endif
item:
        move.l (a4),-(sp)
        move.w d3,-(sp)
        pea 6(a4)
        pea 8(a4)
        pea 12(a4)
        dc.w $a98d
        rts
disable:
        bsr item
        move.l 8(a4),-(sp)
        move.w #255,-(sp)
        dc.w $a95d
        rts
        dc.b 'S7PROBE!'
state:
        ds.b 76
        ifd CONTROL_VARIANTS
        ds.b 4
        endif
        ifd MEMORY_SLIDER
        ds.b 2
        endif
        ifd SOUND_SLIDER
        ds.b 2
        endif
        ifd NETWORK_SLIDER
        ds.b 2
        endif
        ifd SPEECH_CUSTOM
        ds.b 2
        endif
        ifd SPEECH_BUTTONS
        ds.b 4
        endif
        ifd SPEECH_SLIDER
        ds.b 2
        endif
